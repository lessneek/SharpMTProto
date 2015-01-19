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
    using System.Diagnostics;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
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
        IMessageEnvelope Send(object messageBody, MessageSendingFlags flags);
        void SetSessionId(ulong sessionId);
    }

    public class MTProtoSession : Cancelable, IMTProtoSession
    {
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly MTProtoSessionEnvironment _environment = new MTProtoSessionEnvironment();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IRandomGenerator _randomGenerator;
        private readonly ConcurrentDictionary<ulong, IMessageEnvelope> _sentMessages = new ConcurrentDictionary<ulong, IMessageEnvelope>();
        private IAuthInfo _authInfo = new AuthInfo();
        private Subject<IMessageEnvelope> _incomingMessages = new Subject<IMessageEnvelope>();
        private uint _messageSeqNumber;
        private Subject<IMessageEnvelope> _outgoingMessages = new Subject<IMessageEnvelope>();
        private ObservableProperty<IMTProtoSession, MTProtoSessionTag> _sessionTag;

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

            UpdateLastActivity();
        }

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

        public IMessageEnvelope Send(object messageBody, MessageSendingFlags flags)
        {
            ThrowIfDisposed();

            UpdateLastActivity();

            bool isEncrypted = flags.HasFlag(MessageSendingFlags.Encrypted);
            bool isContentRelated = flags.HasFlag(MessageSendingFlags.ContentRelated);

            IMessageEnvelope messageEnvelope = CreateMessageEnvelope(messageBody, isEncrypted, isContentRelated);

            EnqueueToSendingQueue(messageEnvelope);

            return messageEnvelope;
        }

        public void OnNext(IMessageEnvelope messageEnvelope)
        {
            // TODO: check msgId for multiple accepting of one message.
            // TODO: check msgId is not too old or from future.

            if (IsDisposed)
                return;

            UpdateLastActivity();

            _incomingMessages.OnNext(messageEnvelope);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        private void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        private void EnqueueToSendingQueue(IMessageEnvelope messageEnvelope)
        {
            // TODO: enqueue message envelope to outbox queue before sending.
            Task.Run(() =>
            {
                ulong msgId = messageEnvelope.Message.MsgId;

                Debug.Assert(!_sentMessages.ContainsKey(msgId));

                _sentMessages.GetOrAdd(msgId, messageEnvelope);
                _outgoingMessages.OnNext(messageEnvelope);
            });
        }

        private IMessageEnvelope CreateMessageEnvelope(object body, bool isEncrypted, bool isContentRelated)
        {
            var message = new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
            if (isEncrypted)
            {
                return new MessageEnvelope(_sessionTag.Value, _authInfo.Salt, message);
            }
            return new MessageEnvelope(message);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
            }
            base.Dispose(disposing);
        }
    }
}
