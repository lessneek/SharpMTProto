//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
    using SharpMTProto.Messaging.Handlers;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Utils;

    public interface IMTProtoSession
    {
        ulong SessionId { get; set; }
        ulong AuthKeyId { get; }
        IAuthInfo AuthInfo { get; set; }
        IObservable<IMessageEnvelope> OutgoingMessages { get; }
        IObservable<IMessageEnvelope> IncomingMessages { get; }
        void UpdateSalt(ulong salt);
        IMessageEnvelope Send(object messageBody, MessageSendingFlags flags);
        void AcceptIncomingMessage(IMessageEnvelope messageEnvelope);
    }

    public class MTProtoSession : Cancelable, IMTProtoSession
    {
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IRandomGenerator _randomGenerator;
        private readonly ConcurrentDictionary<ulong, IMessageEnvelope> _sentMessages = new ConcurrentDictionary<ulong, IMessageEnvelope>();
        private IAuthInfo _authInfo = new AuthInfo();
        private Subject<IMessageEnvelope> _incomingMessages = new Subject<IMessageEnvelope>();
        private MessageHandlersHub _messageHandlersHub;
        private uint _messageSeqNumber;
        private Subject<IMessageEnvelope> _outgoingMessages = new Subject<IMessageEnvelope>();

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
        }

        public IObservable<IMessageEnvelope> IncomingMessages
        {
            get { return _incomingMessages.AsObservable(); }
        }

        public IObservable<IMessageEnvelope> OutgoingMessages
        {
            get { return _outgoingMessages.AsObservable(); }
        }

        public ulong SessionId { get; set; }
        public ulong AuthKeyId { get; private set; }

        public IAuthInfo AuthInfo
        {
            get { return _authInfo; }
            set
            {
                _authInfo = value;
                AuthKeyId = _authInfo.AuthKey == null ? 0 : _authKeysProvider.ComputeAuthKeyId(_authInfo.AuthKey);
            }
        }

        public void UpdateSalt(ulong salt)
        {
            _authInfo.Salt = salt;
        }

        public void AcceptIncomingMessage(IMessageEnvelope messageEnvelope)
        {
            // TODO: check msgId for multiple accepting of one message.
            // TODO: check msgId is not too old or from future.

            _incomingMessages.OnNext(messageEnvelope);
        }

        public IMessageEnvelope Send(object messageBody, MessageSendingFlags flags)
        {
            bool isEncrypted = flags.HasFlag(MessageSendingFlags.Encrypted);
            bool isContentRelated = flags.HasFlag(MessageSendingFlags.ContentRelated);

            IMessageEnvelope messageEnvelope = CreateMessageEnvelope(messageBody, isEncrypted, isContentRelated);

            // TODO: enqueue message envelope to outbox queue before sending.

            ulong msgId = messageEnvelope.Message.MsgId;
            Debug.Assert(!_sentMessages.ContainsKey(msgId));

            _sentMessages.GetOrAdd(msgId, messageEnvelope);
            _outgoingMessages.OnNext(messageEnvelope);

            return messageEnvelope;
        }

        private IMessageEnvelope CreateMessageEnvelope(object body, bool isEncrypted, bool isContentRelated)
        {
            var message = new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
            if (isEncrypted)
            {
                return new MessageEnvelope(AuthKeyId, SessionId, _authInfo.Salt, message);
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
                if (_messageHandlersHub != null)
                {
                    _messageHandlersHub.Dispose();
                    _messageHandlersHub = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
