// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoMessenger.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Diagnostics;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using Authentication;
    using BigMath.Utils;
    using Dataflows;
    using Messaging;
    using Schema;
    using Services;
    using SharpTL;
    using Transport;
    using Utils;

    /// <summary>
    ///     Interface of a MTProto connection.
    /// </summary>
    public interface IMTProtoMessenger : ICancelable
    {
        /// <summary>
        ///     Is encryption supported.
        /// </summary>
        bool IsEncryptionSupported { get; }

        IClientTransport Transport { get; }
        IObservable<IMessage> IncomingMessages { get; }
        IObservable<IMessage> OutgoingMessages { get; }
        bool HasSession { get; }
        IObservable<Session> SessionUpdates { get; }

        /// <summary>
        ///     Sets auth info.
        /// </summary>
        /// <param name="authInfo">Connection configuration.</param>
        void SetAuthInfo(AuthInfo authInfo);

        /// <summary>
        ///     Updates salt.
        /// </summary>
        /// <param name="salt">New salt.</param>
        void UpdateSalt(ulong salt);

        void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly);
        Task SendAsync(object messageBody, MessageSendingFlags flags);
        Task SendAsync(object messageBody, MessageSendingFlags flags, CancellationToken cancellationToken);
        Task SendAsync(IMessage message, MessageSendingFlags flags);
        Task SendAsync(IMessage message, MessageSendingFlags flags, CancellationToken cancellationToken);
        Message CreateMessage(object body, bool isContentRelated);
        void CreateNewSession();
        void CreateSession(ulong sessionId);
    }

    /// <summary>
    ///     MTProto connection base.
    /// </summary>
    public class MTProtoMessenger : Cancelable, IMTProtoMessenger
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rnd = new Random();
        private AuthInfo _authInfo = AuthInfo.Empty;
        private Subject<IMessage> _incomingMessages = new Subject<IMessage>();
        private uint _messageSeqNumber;
        private Subject<IMessage> _outgoingMessages = new Subject<IMessage>();
        private Session? _session;
        private Subject<Session> _sessionUpdates = new Subject<Session>();
        private IDisposable _transportSubscription;
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IBytesOcean _bytesOcean;
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;

        public MTProtoMessenger([NotNull] IClientTransport transport,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec,
            [NotNull] IAuthKeysProvider authKeysProvider,
            IBytesOcean bytesOcean = null,
            MessengerMode mode = MessengerMode.Client)
        {
            if (transport == null)
                throw new ArgumentNullException("transport");
            if (messageIdGenerator == null)
                throw new ArgumentNullException("messageIdGenerator");
            if (messageCodec == null)
                throw new ArgumentNullException("messageCodec");
            if (authKeysProvider == null)
                throw new ArgumentNullException("authKeysProvider");

            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;
            _authKeysProvider = authKeysProvider;
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultMTProtoMessengerBytesOcean();

            IsServerMode = mode == MessengerMode.Server;

            // Init transport.
            Transport = transport;

            // Connector in/out.
            _transportSubscription = Transport.Do(bytes => LogMessageInOut(bytes, "IN")).Subscribe(ProcessIncomingMessageBytes);
        }

        public bool IsServerMode { get; private set; }

        public IObservable<IMessage> IncomingMessages
        {
            get { return _incomingMessages; }
        }

        public IObservable<IMessage> OutgoingMessages
        {
            get { return _outgoingMessages; }
        }

        public bool IsEncryptionSupported
        {
            get { return _authInfo.HasAuthKey; }
        }

        public bool HasSession
        {
            get { return _session.HasValue; }
        }

        public IClientTransport Transport { get; private set; }

        public void SetAuthInfo(AuthInfo authInfo)
        {
            _authInfo = authInfo;

            CreateNewSession();
        }

        public void CreateNewSession()
        {
            CreateSession(GetNewSessionId());
        }

        public void CreateSession(ulong sessionId)
        {
            ThrowIfEncryptionIsNotSupported("Session available only when encryption is supported.");
            _session = new Session(sessionId, _messageCodec.ComputeAuthKeyId(_authInfo.AuthKey));
            _sessionUpdates.OnNext(_session.Value);
        }

        public void UpdateSalt(ulong salt)
        {
            _authInfo.Salt = salt;
        }

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            _messageCodec.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        public Task SendAsync(object messageBody, MessageSendingFlags flags)
        {
            return SendAsync(messageBody, flags, CancellationToken.None);
        }

        public async Task SendAsync(object messageBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            Message message = CreateMessage(messageBody, flags.HasFlag(MessageSendingFlags.ContentRelated));
            await SendAsync(message, flags, cancellationToken);
        }

        public Task SendAsync(IMessage message, MessageSendingFlags flags)
        {
            return SendAsync(message, flags, CancellationToken.None);
        }

        public Task SendAsync(IMessage message, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                IBytesBucket messageBytesBucket = await _bytesOcean.TakeAsync(MTProtoDefaults.MaximumMessageLength);
                using (var streamer = new TLStreamer(messageBytesBucket.Bytes))
                {
                    await EncodeMessageAsync(message, streamer, flags.HasFlag(MessageSendingFlags.Encrypted));
                    messageBytesBucket.Used = (int) streamer.Position;
                }
                await SendRawDataAsync(messageBytesBucket, cancellationToken);
                _outgoingMessages.OnNext(message);
            },
                cancellationToken);
        }

        public Message CreateMessage(object body, bool isContentRelated)
        {
            return new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
        }

        public IObservable<Session> SessionUpdates
        {
            get { return _sessionUpdates.AsObservable(); }
        }

        protected async Task SendRawDataAsync(IBytesBucket dataBucket, CancellationToken cancellationToken)
        {
            if (IsDisposed)
                return;

            LogMessageInOut(dataBucket, "OUT");
            await Transport.SendAsync(dataBucket, cancellationToken).ConfigureAwait(false);
        }

        private uint GetNextMsgSeqno(bool isContentRelated)
        {
            uint x = (isContentRelated ? 1u : 0);
            uint result = _messageSeqNumber*2 + x;
            _messageSeqNumber += x;
            return result;
        }

        private static ulong GetNewSessionId()
        {
            return ((ulong) Rnd.Next() << 32) + (ulong) Rnd.Next();
        }

        private ulong GetNextMsgId()
        {
            return _messageIdGenerator.GetNextMessageId();
        }

        private static void LogMessageInOut(IBytesBucket messageBytes, string inOrOut)
        {
            ArraySegment<byte> bytes = messageBytes.UsedBytes;
            Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, bytes.Count, bytes.ToHexString()));
        }

        private ulong GetMsgAuthKeyId(TLStreamer streamer)
        {
            long position = streamer.Position;
            if (streamer.Length == 4)
            {
                int error = streamer.ReadInt32();
                throw new MTProtoErrorException(error);
            }
            if (streamer.Length < 20)
            {
                throw new InvalidMessageException(
                    string.Format("Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.",
                        streamer.Length));
            }
            ulong incomingMsgAuthKeyId = streamer.ReadUInt64();
            streamer.Position = position;
            return incomingMsgAuthKeyId;
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="messageBucket">Incoming bytes in a bucket.</param>
        private async void ProcessIncomingMessageBytes(IBytesBucket messageBucket)
        {
            if (IsDisposed)
                return;

            try
            {
                Debug("Processing incoming message.");

                using (messageBucket)
                using (var streamer = new TLStreamer(messageBucket.UsedBytes))
                {
                    IMessage message;

                    ulong incomingMsgAuthKeyId = GetMsgAuthKeyId(streamer);
                    if (incomingMsgAuthKeyId == 0)
                    {
                        // Assume the message bytes has a plain (unencrypted) message.
                        Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is a plain (unencrypted) message.", incomingMsgAuthKeyId));

                        message = await _messageCodec.DecodePlainMessageAsync(streamer);
                    }
                    else
                    {
                        // Assume the stream has an encrypted message.
                        Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is encrypted message.", incomingMsgAuthKeyId));

                        if (IsServerMode)
                        {
                            AuthKeyWithId incomingMsgAuthKeyWithId;
                            if (!_authKeysProvider.TryGet(incomingMsgAuthKeyId, out incomingMsgAuthKeyWithId))
                            {
                                throw new InvalidMessageException(
                                    string.Format("Unable to decrypt incoming message with auth key ID '{0}'. Auth key with such ID not found.",
                                        incomingMsgAuthKeyId));
                            }

                            if (_session.HasValue)
                            {
                                // Already authorized.
                                if (incomingMsgAuthKeyWithId.Id != _session.Value.AuthKeyId)
                                {
                                    throw new InvalidMessageException(
                                        string.Format(
                                            "Connection has already initialized with auth key ID '{0}', but message with auth key ID '{1}' is accepted.",
                                            _session.Value.AuthKeyId,
                                            incomingMsgAuthKeyId));
                                }
                            }
                            else
                            {
                                // Init authorization.
                                _authInfo.AuthKey = incomingMsgAuthKeyWithId.Value;
                            }
                        }
                        else // Client mode.
                        {
                            if (!IsEncryptionSupported)
                            {
                                Debug("Encryption is not supported by this connection.");
                                return;
                            }
                        }

                        // Decoding an encrypted message.
                        MessageEnvelope messageEnvelope =
                            await
                                _messageCodec.DecodeEncryptedMessageAsync(streamer,
                                    _authInfo.AuthKey,
                                    IsServerMode ? MessengerMode.Client : MessengerMode.Server);

                        // TODO: check salt.
                        // _authInfo.Salt == messageEnvelope.Salt;

                        message = messageEnvelope.Message;

                        if (IsServerMode)
                        {
                            if (!_session.HasValue)
                            {
                                // If there is no client session on a server, then create it.
                                CreateSession(messageEnvelope.SessionId);
                            }
                        }
                        else // Client mode.
                        {
                            if (!_session.HasValue)
                            {
                                throw new InvalidMessageException(string.Format("Received a message for session ID {0}, but there is no session.",
                                    messageEnvelope.SessionId));
                            }
                            if (messageEnvelope.SessionId != _session.Value.Id)
                            {
                                throw new InvalidMessageException(string.Format("Invalid session ID {0}. Expected {1}.",
                                    messageEnvelope.SessionId,
                                    _session.Value.Id));
                            }
                        }
                        Debug(string.Format("Received encrypted message. Message ID = 0x{0:X16}.", message.MsgId));
                    }

                    ProcessIncomingMessage(message);
                }
            }
            catch (MTProtoException e)
            {
                Debug(e.Message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to receive a message.");
            }
        }

        private void ProcessIncomingMessage(IMessage message)
        {
            if (IsDisposed)
            {
                return;
            }
            try
            {
                if (!IsIncomingMessageIdValid(message.MsgId))
                {
                    throw new InvalidMessageException(string.Format("Message ID = 0x{0:X16} is invalid.", message.MsgId));
                }

                Debug(string.Format("Incoming message data of type = {0}.", message.Body.GetType()));

                _incomingMessages.OnNext(message);
            }
            catch (Exception e)
            {
                Debug(string.Format("Error while processing incoming message. {0}", e.Message));
            }
        }

        [Conditional("DEBUG")]
        private static void Debug(string message)
        {
            Log.Debug(string.Format("[MTProtoMessenger] : {0}", message));
        }

        protected bool IsIncomingMessageIdValid(ulong messageId)
        {
            // TODO: check.
            return true;
        }

        protected Task EncodeMessageAsync(IMessage message, TLStreamer streamer, bool isEncrypted)
        {
            if (isEncrypted)
            {
                ThrowIfEncryptionIsNotSupported();
                if (!_session.HasValue)
                    throw new InvalidOperationException("Unable to encode encrypted message without a session ID.");

                ulong authKeyId = _messageCodec.ComputeAuthKeyId(_authInfo.AuthKey);
                var messageEnvelope = new MessageEnvelope(authKeyId, _session.Value.Id, _authInfo.Salt, message);

                return _messageCodec.EncodeEncryptedMessageAsync(messageEnvelope,
                    streamer,
                    _authInfo.AuthKey,
                    IsServerMode ? MessengerMode.Server : MessengerMode.Client);
            }
            return _messageCodec.EncodePlainMessageAsync(message, streamer);
        }

        [DebuggerStepThrough]
        protected void ThrowIfEncryptionIsNotSupported(string message = null)
        {
            if (!IsEncryptionSupported)
            {
                string text = string.Format("{0} Setup encryption first by calling SetAuthInfo() method.", message ?? "Encryption is not supported.");
                throw new InvalidOperationException(text);
            }
        }

        #region Disposable

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_sessionUpdates != null)
                {
                    _sessionUpdates.Dispose();
                    _sessionUpdates = null;
                }
                if (_transportSubscription != null)
                {
                    _transportSubscription.Dispose();
                    _transportSubscription = null;
                }
                if (Transport != null)
                {
                    Transport.Dispose();
                    Transport = null;
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
            }
            base.Dispose(isDisposing);
        }

        #endregion
    }
}
