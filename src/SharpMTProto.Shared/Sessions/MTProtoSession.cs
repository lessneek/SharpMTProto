//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UnusedMember.Global

#endregion

namespace SharpMTProto.Sessions
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Sessions.Modules;
    using SharpMTProto.Transport;
    using SharpMTProto.Utils;

    public class MTProtoSessionEnvironment : ConcurrentDictionary<string, object>
    {
        public T GetOrCreate<T>(string key) where T : class, new()
        {
            return GetOrAdd(key, s => new T()) as T;
        }
    }

    public interface IMTProtoSession : ICancelable
    {
        ulong SessionId { get; set; }
        IAuthInfo AuthInfo { get; set; }
        IObservable<MovingMessageEnvelope> OutgoingMessages { get; }
        IObservable<MovingMessageEnvelope> IncomingMessages { get; }
        IObservableReadonlyProperty<IMTProtoSession, MTProtoSessionTag> SessionTag { get; }
        DateTime LastActivity { get; }
        MTProtoSessionEnvironment Environment { get; }
        void UpdateSalt(ulong salt);
        ulong EnqueueToSend(object messageBody, bool isContentRelated, bool isEncrypted);
        bool TryGetSentMessage(ulong msgId, out IMessage message);

        /// <summary>
        ///     Processes next incoming message.
        /// </summary>
        /// <param name="incoming">An incoming message envelope.</param>
        Task ProcessIncomingMessageAsync(MovingMessageEnvelope incoming);

        void AddModule(ISessionModule module, bool dispose = true);
        void RemoveModule(ISessionModule module);
    }

    public abstract class MTProtoSession : Cancelable, IMTProtoSession
    {
        protected static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IRandomGenerator _randomGenerator;
        
        private uint _messageSeqNumber;
        private IAuthInfo _authInfo = new AuthInfo();
        private ObservableProperty<IMTProtoSession, MTProtoSessionTag> _sessionTag;
        private readonly MTProtoSessionEnvironment _environment = new MTProtoSessionEnvironment();

        private readonly SortedSet<ulong> _receivedMsgIds = new SortedSet<ulong>();
        private readonly ConcurrentDictionary<ulong, IMessageEnvelope> _sentMessages = new ConcurrentDictionary<ulong, IMessageEnvelope>();

        private readonly ConcurrentQueue<Message> _encryptedMessagesToSend = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> _plainMessagesToSend = new ConcurrentQueue<Message>();

        private readonly ConcurrentQueue<ulong> _msgIdsToAcknowledge = new ConcurrentQueue<ulong>();

        private Subject<MovingMessageEnvelope> _incomingMessages = new Subject<MovingMessageEnvelope>();
        private Subject<MovingMessageEnvelope> _outgoingMessages = new Subject<MovingMessageEnvelope>();

        private readonly AsyncLock _sendingAsyncLock = new AsyncLock();
        private CancellationTokenSource _sessionCancellationTokenSource = new CancellationTokenSource();

        public MTProtoSession([NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IRandomGenerator randomGenerator,
            [NotNull] IAuthKeysProvider authKeysProvider)
        {
            if (messageIdGenerator == null)
                throw new ArgumentNullException("messageIdGenerator");
            if (randomGenerator == null)
                throw new ArgumentNullException("randomGenerator");
            if (authKeysProvider == null)
                throw new ArgumentNullException("authKeysProvider");

            _messageIdGenerator = messageIdGenerator;
            _randomGenerator = randomGenerator;
            _authKeysProvider = authKeysProvider;

            _sessionTag = new ObservableProperty<IMTProtoSession, MTProtoSessionTag>(this) {Value = MTProtoSessionTag.Empty};
            SendAsyncFunc = (s, e, c) => Task.FromResult(false);

            AcknowledgeInterval = TimeSpan.FromSeconds(25);
            MaxDelay = TimeSpan.FromMilliseconds(50);

            UpdateLastActivity();

            StartSchedulers(_sessionCancellationTokenSource.Token);
        }

        public TimeSpan AcknowledgeInterval { get; set; }
        public TimeSpan MaxDelay { get; set; }

        public IObservable<MovingMessageEnvelope> IncomingMessages
        {
            get { return _incomingMessages.AsObservable(); }
        }

        public IObservable<MovingMessageEnvelope> OutgoingMessages
        {
            get { return _outgoingMessages.AsObservable(); }
        }

        public IObservableReadonlyProperty<IMTProtoSession, MTProtoSessionTag> SessionTag
        {
            get { return _sessionTag.AsReadonly; }
        }

        public IAuthInfo AuthInfo
        {
            get { return _authInfo; }
            set
            {
                if (IsDisposed)
                    return;

                _authInfo = value;
                ulong authKeyId = _authInfo.AuthKey == null ? 0 : _authKeysProvider.Add(_authInfo.AuthKey).AuthKeyId;
                _sessionTag.Value = SessionTag.Value.UpdateAuthKeyId(authKeyId);
            }
        }

        public ulong SessionId
        {
            get
            {
                ThrowIfDisposed();
                return _sessionTag.Value.SessionId;
            }
            set
            {
                if (IsDisposed)
                    return;

                _sessionTag.Value = _sessionTag.Value.UpdateSessionId(value);
            }
        }

        public DateTime LastActivity { get; private set; }

        public MTProtoSessionEnvironment Environment
        {
            get { return _environment; }
        }

        public Func<IMTProtoSession, IMessageEnvelope, CancellationToken, Task<bool>> SendAsyncFunc { get; set; }

        #region Modules

        private ImmutableArray<ISessionModule> _modules = ImmutableArray<ISessionModule>.Empty;
        private ImmutableArray<IDisposable> _modulesToDispose = ImmutableArray<IDisposable>.Empty;
        private readonly object _modulesSyncRoot = new object();

        public ImmutableArray<ISessionModule> Modules
        {
            get { return _modules; }
        }

        public void AddModule(ISessionModule module, bool dispose = true)
        {
            lock (_modulesSyncRoot)
            {
                if (!_modules.Contains(module))
                {
                    _modules = _modules.Add(module);

                    if (dispose)
                        _modulesToDispose = _modulesToDispose.Add(module);
                }
            }
        }

        public void RemoveModule(ISessionModule module)
        {
            lock (_modulesSyncRoot)
            {
                if (_modules.Contains(module))
                    _modules = _modules.Remove(module);

                if (_modulesToDispose.Contains(module))
                    _modulesToDispose = _modulesToDispose.Remove(module);
            }
        }

        #endregion

        public void UpdateSalt(ulong salt)
        {
            if (IsDisposed)
                return;

            _authInfo.Salt = salt;
        }

        public ulong EnqueueToSend(object messageBody, bool isContentRelated, bool isEncrypted)
        {
            ThrowIfDisposed();
            UpdateLastActivity();

            Message message = CreateMessage(messageBody, isContentRelated);

            if (isEncrypted)
                _encryptedMessagesToSend.Enqueue(message);
            else
                _plainMessagesToSend.Enqueue(message);

            // Trigger sending of the whole messages queue.
            SendAllQueuedAsync(_sessionCancellationTokenSource.Token);

            return message.MsgId;
        }

        public bool TryGetSentMessage(ulong msgId, out IMessage message)
        {
            message = null;
            IMessageEnvelope messageEnvelope;
            if (!_sentMessages.TryGetValue(msgId, out messageEnvelope))
                return false;

            message = messageEnvelope.Message;
            return true;
        }

        protected virtual void BindClientTransport(IClientTransport clientTransport)
        {
        }

        protected CancellationToken SessionCancellationToken
        {
            get { return _sessionCancellationTokenSource.Token; }
        }

        /// <summary>
        ///     Processes next incoming message.
        /// </summary>
        /// <param name="incoming">An incoming message envelope.</param>
        public async Task ProcessIncomingMessageAsync(MovingMessageEnvelope incoming)
        {
            if (IsDisposed)
                return;

            UpdateLastActivity();

            IClientTransport clientTransport = incoming.ClientTransport;

            BindClientTransport(clientTransport);

            ImmutableArray<ISessionModule> modules = _modules;
            ImmutableArray<IMessageEnvelope> allMessages = ExtractAllMessages(incoming.MessageEnvelope);

            foreach (MovingMessageEnvelope inMsgEnv in from messageEnvelope in allMessages
                where StateAndCheckIncomingMessage(messageEnvelope)
                select new MovingMessageEnvelope(clientTransport, messageEnvelope))
            {
                foreach (ISessionModule module in modules)
                {
                    try
                    {
                        await module.ProcessIncomingMessageAsync(this, inMsgEnv).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, string.Format("Error while processing message within a module: '{0}'.", module.GetType()));
                    }
                }
                _incomingMessages.OnNext(inMsgEnv);
            }
        }

        private static ImmutableArray<IMessageEnvelope> ExtractAllMessages(IMessageEnvelope messageEnvelope)
        {
            IMessage message = messageEnvelope.Message;

            var msgContainer = message.Body as MsgContainer;
            if (msgContainer != null)
            {
                #region About containers

                /*
                 * All messages in a container must have msg_id lower than that of the container itself.
                 * A container does not require an acknowledgment and may not carry other simple containers.
                 * When messages are re-sent, they may be combined into a container in a different manner or sent individually.
                 * 
                 * Empty containers are also allowed. They are used by the server, for example,
                 * to respond to an HTTP request when the timeout specified in hhtp_wait expires, and there are no messages to transmit.
                 * 
                 * https://core.telegram.org/mtproto/service_messages#containers
                 */

                #endregion

                if (msgContainer.Messages.Any(msg => msg.MsgId >= message.MsgId || msg.Seqno > message.Seqno))
                {
                    throw new InvalidMessageException("Container MessageId must be greater than all MsgIds of inner messages.");
                }

                return
                    msgContainer.Messages.Select(
                        msg => (IMessageEnvelope) MessageEnvelope.CreateEncrypted(messageEnvelope.SessionTag, messageEnvelope.Salt, msg))
                        .ToImmutableArray();
            }

            return ImmutableArray.Create(messageEnvelope);
        }

        private void StartSchedulers(CancellationToken cancellationToken)
        {
            Observable.Interval(AcknowledgeInterval).Subscribe(async l =>
            {
                ImmutableArray<ulong> dequeueAllMsgsAckToSend = DequeueAllMsgsAckToSend();

                if (dequeueAllMsgsAckToSend.Length == 0)
                    return;

                EnqueueToSend(new MsgsAck {MsgIds = dequeueAllMsgsAckToSend.ToList()}, false, true);
                await SendAllQueuedAsync(cancellationToken).ConfigureAwait(false);
            },
                cancellationToken);
        }

        private async Task SendAllQueuedAsync(CancellationToken cancellationToken)
        {
            using (await _sendingAsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(MaxDelay, cancellationToken).ConfigureAwait(false);

                ImmutableArray<Message> plainMessages = DequeueAllMessagesToSend(false);
                ImmutableArray<Message> encryptedMessages = DequeueAllMessagesToSend(true);

                // Send all plain messages separately.
                int plainMessagesLength = plainMessages.Length;
                for (var i = 0; i < plainMessagesLength; i++)
                {
                    Message plainMsg = plainMessages[i];
                    if (!await SendAsync(CreateMessageEnvelope(plainMsg, false), cancellationToken).ConfigureAwait(false))
                        EnqueueToResend(plainMsg, false);
                }

                if (encryptedMessages.Length > 0)
                {
                    // Send single encrpted message or all encrypted messages in a container.
                    Message encryptedMessage = encryptedMessages.Length == 1
                        ? encryptedMessages.Single()
                        : CreateMessage(new MsgContainer {Messages = encryptedMessages.ToList()}, false);

                    if (!await SendAsync(CreateMessageEnvelope(encryptedMessage, true), cancellationToken).ConfigureAwait(false))
                        EnqueueToResend(encryptedMessages, true);
                }
            }
        }

        private void EnqueueToResend(ImmutableArray<Message> messagesToResend, bool encrypted)
        {
            ConcurrentQueue<Message> queue = encrypted ? _encryptedMessagesToSend : _plainMessagesToSend;

            for (var i = 0; i < messagesToResend.Length; i++)                
                queue.Enqueue(messagesToResend[i]);
        }

        private void EnqueueToResend(Message messageToResend, bool encrypted)
        {
            (encrypted ? _encryptedMessagesToSend : _plainMessagesToSend).Enqueue(messageToResend);
        }

        private ImmutableArray<Message> DequeueAllMessagesToSend(bool encrypted)
        {
            ConcurrentQueue<Message> queue = encrypted ? _encryptedMessagesToSend : _plainMessagesToSend;
            var messageEnvelopes = ImmutableArray.CreateBuilder<Message>(queue.Count);
            
            Message message;
            while (queue.TryDequeue(out message))
                messageEnvelopes.Add(message);

            return messageEnvelopes.ToImmutable();
        }

        private ImmutableArray<ulong> DequeueAllMsgsAckToSend()
        {
            var msgIds = ImmutableArray.CreateBuilder<ulong>(_msgIdsToAcknowledge.Count);

            ulong msgId;
            while (_msgIdsToAcknowledge.TryDequeue(out msgId))
                msgIds.Add(msgId);

            return msgIds.ToImmutable();
        }

        private void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        protected abstract Task<MovingMessageEnvelope> SendInternalAsync(IMessageEnvelope messageEnvelope,
            CancellationToken cancellationToken = new CancellationToken());

        private async Task<bool> SendAsync(IMessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        {
            ulong msgId = messageEnvelope.Message.MsgId;

            if (_sentMessages.ContainsKey(msgId))
            {
                Log.Warning(string.Format("Message {0} already sent. Sending is ignored.", msgId));
                return true;
            }

            try
            {
                MovingMessageEnvelope movingMessageEnvelope = await SendInternalAsync(messageEnvelope, cancellationToken).ConfigureAwait(false);

                _sentMessages.GetOrAdd(msgId, messageEnvelope);
                _outgoingMessages.OnNext(movingMessageEnvelope);

                return true;
            }
            catch (Exception e)
            {
                Log.Warning(e.Message);
            }
            return false;
        }

        protected Message CreateMessage(object body, bool isContentRelated)
        {
            return new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
        }

        protected IMessageEnvelope CreateMessageEnvelope(object body, bool isContentRelated, bool isEncrypted)
        {
            return CreateMessageEnvelope(CreateMessage(body, isContentRelated), isEncrypted);
        }

        protected IMessageEnvelope CreateMessageEnvelope(IMessage message, bool isEncrypted)
        {
            return isEncrypted ? MessageEnvelope.CreateEncrypted(_sessionTag.Value, _authInfo.Salt, message) : MessageEnvelope.CreatePlain(message);
        }

        private ulong GetNewSessionId()
        {
            return _randomGenerator.NextUInt64();
        }

        private ulong GetNextMsgId()
        {
            return _messageIdGenerator.GetNextMessageId();
        }

        private uint GetNextMsgSeqno(bool isContentRelated)
        {
            uint x = (isContentRelated ? 1u : 0);
            uint result = _messageSeqNumber*2 + x;
            _messageSeqNumber += x;
            return result;
        }

        /// <summary>
        ///     States and checks received incoming messages.
        /// </summary>
        /// <param name="incomingMessageEnvelope">Message envelope.</param>
        /// <returns>True - when all checks are passed and message can be processed. False - message needs to be ignored.</returns>
        private bool StateAndCheckIncomingMessage(IMessageEnvelope incomingMessageEnvelope)
        {
            ulong msgId = incomingMessageEnvelope.Message.MsgId;

            // Validate a message id.
            if (!IsMsgIdValid(msgId))
                return false;

            lock (_receivedMsgIds)
            {
                // Ignore duplicates.
                if (_receivedMsgIds.Contains(msgId))
                    return false;

                _receivedMsgIds.Add(msgId);
            }

            if (incomingMessageEnvelope.IsEncryptedAndContentRelated())
                _msgIdsToAcknowledge.Enqueue(msgId);

            return true;
        }

        private bool IsMsgIdValid(ulong msgId)
        {
            // TODO: implement msgId validation.
            // TODO: check msgId is not too old or from future.
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sessionCancellationTokenSource != null)
                {
                    _sessionCancellationTokenSource.Cancel();
                    _sessionCancellationTokenSource.Dispose();
                    _sessionCancellationTokenSource = null;
                }
                if (_incomingMessages != null)
                {
                    _incomingMessages.OnCompleted();
                    _incomingMessages.Dispose();
                    _incomingMessages = null;
                }
                if (_outgoingMessages != null)
                {
                    _outgoingMessages.OnCompleted();
                    _outgoingMessages.Dispose();
                    _outgoingMessages = null;
                }
                if (_sessionTag != null)
                {
                    _sessionTag.Dispose();
                    _sessionTag = null;
                }
                lock (_receivedMsgIds)
                {
                    _receivedMsgIds.Clear();
                }
                lock (_modulesSyncRoot)
                {
                    _modules = ImmutableArray<ISessionModule>.Empty;
                    
                    foreach (var disposable in _modulesToDispose)
                        disposable.Dispose();
                    _modulesToDispose = ImmutableArray<IDisposable>.Empty;
                }
            }
            base.Dispose(disposing);
        }
    }
}
