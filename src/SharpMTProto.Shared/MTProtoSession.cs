//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UnusedMember.Global

#endregion

namespace SharpMTProto
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Utils;

    public class MTProtoSessionEnvironment : ConcurrentDictionary<string, object>
    {
        public T GetOrCreate<T>(string key) where T : class, new()
        {
            return GetOrAdd(key, s => new T()) as T;
        }
    }

    public interface IMTProtoSession : IObserver<IMessageEnvelope>, ICancelable
    {
        IAuthInfo AuthInfo { get; set; }
        IObservable<IMessageEnvelope> OutgoingMessages { get; }
        IObservable<IMessageEnvelope> IncomingMessages { get; }
        IObservableReadonlyProperty<IMTProtoSession, MTProtoSessionTag> SessionTag { get; }
        DateTime LastActivity { get; }
        MTProtoSessionEnvironment Environment { get; }
        void UpdateSalt(ulong salt);
        ulong EnqueueToSend(object messageBody, bool isContentRelated, bool isEncrypted);
        void SetSessionId(ulong sessionId);
        bool TryGetSentMessage(ulong msgId, out IMessage message);
    }

    public struct MessageToSend
    {
        public MessageToSend(Message message, bool isEncrypted) : this()
        {
            Message = message;
            IsEncrypted = isEncrypted;
        }

        public Message Message { get; private set; }
        public bool IsEncrypted { get; private set; }
    }

    public class MTProtoSession : Cancelable, IMTProtoSession
    {
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IRandomGenerator _randomGenerator;

        private uint _messageSeqNumber;
        private IAuthInfo _authInfo = new AuthInfo();
        private ObservableProperty<IMTProtoSession, MTProtoSessionTag> _sessionTag;
        private readonly MTProtoSessionEnvironment _environment = new MTProtoSessionEnvironment();

        private readonly SortedSet<ulong> _receivedMsgIds = new SortedSet<ulong>();
        private readonly ConcurrentDictionary<ulong, IMessageEnvelope> _sentMessages = new ConcurrentDictionary<ulong, IMessageEnvelope>();

        private readonly ConcurrentQueue<MessageToSend> _messagesToSend = new ConcurrentQueue<MessageToSend>();
        private readonly ConcurrentQueue<ulong> _msgIdsToAcknowledge = new ConcurrentQueue<ulong>();

        private Subject<IMessageEnvelope> _incomingMessages = new Subject<IMessageEnvelope>();
        private Subject<IMessageEnvelope> _outgoingMessages = new Subject<IMessageEnvelope>();

        private readonly AsyncLock _sendingAsyncLock = new AsyncLock();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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

            AcknowledgeInterval = TimeSpan.FromSeconds(25);
            SendingInterval = TimeSpan.FromMilliseconds(50);

            UpdateLastActivity();

            StartSchedulers(_cancellationTokenSource.Token);
        }

        public TimeSpan AcknowledgeInterval { get; set; }
        public TimeSpan SendingInterval { get; set; }

        public IObservable<IMessageEnvelope> IncomingMessages
        {
            get { return _incomingMessages.AsObservable(); }
        }

        public IObservable<IMessageEnvelope> OutgoingMessages
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
                _authInfo = value;
                ulong authKeyId = _authInfo.AuthKey == null ? 0 : _authKeysProvider.Add(_authInfo.AuthKey).AuthKeyId;
                _sessionTag.Value = SessionTag.Value.UpdateAuthKeyId(authKeyId);
            }
        }

        public void SetSessionId(ulong sessionId)
        {
            _sessionTag.Value = _sessionTag.Value.UpdateSessionId(sessionId);
        }

        public DateTime LastActivity { get; private set; }

        public MTProtoSessionEnvironment Environment
        {
            get { return _environment; }
        }

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

            _messagesToSend.Enqueue(new MessageToSend(message, isEncrypted));

            // Trigger sending of the whole messages queue.
            SendAllQueuedAsync();

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

        /// <summary>
        ///     Processes next incoming message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope.</param>
        public void OnNext(IMessageEnvelope messageEnvelope)
        {
            if (IsDisposed)
                return;

            ulong msgId = messageEnvelope.Message.MsgId;

            // Validate a message id.
            if (!IsMsgIdValid(msgId))
                return;

            lock (_receivedMsgIds)
            {
                // Ignore duplicates.
                if (_receivedMsgIds.Contains(msgId))
                    return;

                _receivedMsgIds.Add(msgId);
            }

            if (messageEnvelope.IsEncrypted && IsContentRelated(messageEnvelope.Message.Seqno))
                _msgIdsToAcknowledge.Enqueue(msgId);

            UpdateLastActivity();

            _incomingMessages.OnNext(messageEnvelope);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        private void StartSchedulers(CancellationToken cancellationToken)
        {
            Observable.Interval(AcknowledgeInterval).Subscribe(l =>
            {
                EnqueueToSend(new MsgsAck {MsgIds = DequeueAllMsgsAckToSend()}, true, true);
                SendAllQueuedAsync();
            },
                cancellationToken);
        }

        private Task SendAllQueuedAsync()
        {
            return Task.Run(async () =>
            {
                using (await _sendingAsyncLock.LockAsync())
                {
                    await Task.Delay(SendingInterval);

                    List<MessageToSend> messagesToSend = DequeueAllMessagesToSend();

                    if (messagesToSend.Count == 0)
                        return;

                    var plainMessages = messagesToSend.Where(mts => !mts.IsEncrypted).Select(mts => mts.Message).ToList();
                    var encryptedMessages = messagesToSend.Where(mts => mts.IsEncrypted).Select(mts => mts.Message).ToList();

                    if (plainMessages.Count > 0)
                    {
                        // Send all plain messages separately.
                        foreach (var plainMsg in plainMessages)
                        {
                            Send(CreateMessageEnvelope(plainMsg, false));
                        }
                    }

                    if (encryptedMessages.Count > 0)
                    {
                        // Send single encrpted message or all encrypted messages in a container.
                        Message encryptedMessage = encryptedMessages.Count == 1
                            ? encryptedMessages.Single()
                            : CreateMessage(new MsgContainer {Messages = encryptedMessages}, false);

                        IMessageEnvelope messageEnvelope = CreateMessageEnvelope(encryptedMessage, true);

                        Send(messageEnvelope);
                    }
                }
            });
        }

        private List<ulong> DequeueAllMsgsAckToSend()
        {
            var msgIds = new List<ulong>(_msgIdsToAcknowledge.Count);
            ulong msgId;
            while (_msgIdsToAcknowledge.TryDequeue(out msgId))
            {
                msgIds.Add(msgId);
            }
            return msgIds;
        }

        private List<MessageToSend> DequeueAllMessagesToSend()
        {
            var messageEnvelopes = new List<MessageToSend>();
            MessageToSend messageToSend;
            while (_messagesToSend.TryDequeue(out messageToSend))
            {
                messageEnvelopes.Add(messageToSend);
            }
            return messageEnvelopes;
        }

        private void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        private void Send(IMessageEnvelope messageEnvelope)
        {
            ulong msgId = messageEnvelope.Message.MsgId;

            Debug.Assert(!_sentMessages.ContainsKey(msgId));

            _sentMessages.GetOrAdd(msgId, messageEnvelope);
            _outgoingMessages.OnNext(messageEnvelope);
        }

        private Message CreateMessage(object body, bool isContentRelated)
        {
            return new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
        }

        private IMessageEnvelope CreateMessageEnvelope(object body, bool isContentRelated, bool isEncrypted)
        {
            return CreateMessageEnvelope(CreateMessage(body, isContentRelated), isEncrypted);
        }

        private IMessageEnvelope CreateMessageEnvelope(IMessage message, bool isEncrypted)
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

        private bool IsContentRelated(ulong seqno)
        {
            return (seqno%2) == 1;
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
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
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
            }
            base.Dispose(disposing);
        }
    }
}
