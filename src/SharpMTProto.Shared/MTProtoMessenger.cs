// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoMessenger.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using BigMath.Utils;
    using Catel.Logging;
    using Messaging;
    using Messaging.Handlers;
    using Schema;
    using Services;
    using SharpTL;
    using Transport;
    using AsyncLock = Nito.AsyncEx.AsyncLock;

    /// <summary>
    ///     Interface of a MTProto connection.
    /// </summary>
    public interface IMTProtoMessenger : IDisposable
    {
        /// <summary>
        ///     Is encryption supported.
        /// </summary>
        bool IsEncryptionSupported { get; }

        IMessageDispatcher IncomingMessageDispatcher { get; }

        IClientTransport Transport { get; }

        /// <summary>
        ///     Configure connection.
        /// </summary>
        /// <param name="config">Connection configuration.</param>
        void Configure(ConnectionConfig config);

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
    }

    /// <summary>
    ///     MTProto connection base.
    /// </summary>
    public class MTProtoMessenger : IMTProtoMessenger
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rnd = new Random();
        private readonly AsyncLock _connectionLock = new AsyncLock();
        private readonly Dictionary<Type, IMessageHandler> _handlers = new Dictionary<Type, IMessageHandler>();
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageDispatcher _messageDispatcher = new MessageDispatcher();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private IClientTransport _transport;
        private ConnectionConfig _config = new ConnectionConfig(null, 0);
        private bool _isDisposed;
        private uint _messageSeqNumber;

        public MTProtoMessenger([NotNull] IClientTransport transport,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec)
        {
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            if (messageIdGenerator == null)
            {
                throw new ArgumentNullException("messageIdGenerator");
            }
            if (messageCodec == null)
            {
                throw new ArgumentNullException("messageCodec");
            }

            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;

            // Init transport.
            _transport = transport;

            // Connector in/out.
            _transport.ObserveOn(DefaultScheduler.Instance)
                .Do(bytes => LogMessageInOut(bytes, "IN"))
                .Subscribe(ProcessIncomingMessageBytes);
        }

        public IMessageDispatcher IncomingMessageDispatcher
        {
            get { return _messageDispatcher; }
        }
        
        public bool IsEncryptionSupported
        {
            get { return _config.AuthKey != null; }
        }

        public IClientTransport Transport
        {
            get { return _transport; }
        }

        /// <summary>
        ///     Set config.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">The <paramref name="config" /> is <c>null</c>.</exception>
        public void Configure([NotNull] ConnectionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;
            if (_config.SessionId == 0)
            {
                _config.SessionId = GetNextSessionId();
            }
        }

        public void UpdateSalt(ulong salt)
        {
            _config.Salt = salt;
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
            byte[] messageBytes = EncodeMessage(CreateMessage(messageBody, flags.HasFlag(MessageSendingFlags.ContentRelated)),
                flags.HasFlag(MessageSendingFlags.Encrypted));

            await SendRawDataAsync(messageBytes, cancellationToken);
        }

        public Task SendAsync(IMessage message, MessageSendingFlags flags)
        {
            return SendAsync(message, flags, CancellationToken.None);
        }

        public Task SendAsync(IMessage message, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                byte[] messageBytes = EncodeMessage(message, flags.HasFlag(MessageSendingFlags.Encrypted));
                await SendRawDataAsync(messageBytes, cancellationToken);
            },
                cancellationToken);
        }

        public Message CreateMessage(object body, bool isContentRelated)
        {
            return new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
        }

        [DebuggerStepThrough]
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
            }
        }

        protected Task SendRawDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDiconnected();
            LogMessageInOut(data, "OUT");
            return _transport.SendAsync(data, cancellationToken);
        }

        private uint GetNextMsgSeqno(bool isContentRelated)
        {
            uint x = (isContentRelated ? 1u : 0);
            uint result = _messageSeqNumber*2 + x;
            _messageSeqNumber += x;
            return result;
        }

        private static ulong GetNextSessionId()
        {
            return ((ulong) Rnd.Next() << 32) + (ulong) Rnd.Next();
        }

        private ulong GetNextMsgId()
        {
            return _messageIdGenerator.GetNextMessageId();
        }

        private static void LogMessageInOut(byte[] messageBytes, string inOrOut)
        {
            Log.Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, messageBytes.Length, messageBytes.ToHexString()));
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="messageBytes">Incoming bytes.</param>
        private void ProcessIncomingMessageBytes(byte[] messageBytes)
        {
            ThrowIfDisposed();

            try
            {
                Log.Debug("Processing incoming message.");
                ulong authKeyId;
                using (var streamer = new TLStreamer(messageBytes))
                {
                    if (messageBytes.Length == 4)
                    {
                        int error = streamer.ReadInt32();
                        Log.Debug("Received error code: {0}.", error);
                        return;
                    }
                    if (messageBytes.Length < 20)
                    {
                        throw new InvalidMessageException(
                            string.Format(
                                "Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.",
                                messageBytes.Length));
                    }
                    authKeyId = streamer.ReadUInt64();
                }

                IMessage message;

                if (authKeyId == 0)
                {
                    // Assume the message bytes has a plain (unencrypted) message.
                    Log.Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is a plain (unencrypted) message.", authKeyId));

                    message = _messageCodec.DecodePlainMessage(messageBytes);

                    if (!IsIncomingMessageIdValid(message.MsgId))
                    {
                        throw new InvalidMessageException(string.Format("Message ID = 0x{0:X16} is invalid.", message.MsgId));
                    }
                }
                else
                {
                    // Assume the stream has an encrypted message.
                    Log.Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is encrypted message.", authKeyId));
                    if (!IsEncryptionSupported)
                    {
                        Log.Debug("Encryption is not supported by this connection.");
                        return;
                    }

                    ulong salt, sessionId;
                    message = _messageCodec.DecodeEncryptedMessage(messageBytes, _config.AuthKey, Sender.Server, out salt, out sessionId);
                    // TODO: check salt.
                    if (sessionId != _config.SessionId)
                    {
                        throw new InvalidMessageException(string.Format("Invalid session ID {0}. Expected {1}.",
                            sessionId,
                            _config.SessionId));
                    }
                    Log.Debug(string.Format("Received encrypted message. Message ID = 0x{0:X16}.", message.MsgId));
                }
                ProcessIncomingMessage(message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to receive a message.");
            }
        }

        private void ProcessIncomingMessage(IMessage message)
        {
            ThrowIfDisposed();

            try
            {
                Log.Debug("Incoming message data of type = {0}.", message.Body.GetType());

                Task.Run(() => _messageDispatcher.DispatchAsync(message));
            }
            catch (Exception e)
            {
                Log.Debug(e, "Error while processing incoming message.");
            }
        }

        protected bool IsIncomingMessageIdValid(ulong messageId)
        {
            // TODO: check.
            return true;
        }

        protected byte[] EncodeMessage(IMessage message, bool isEncrypted)
        {
            if (isEncrypted)
            {
                ThrowIfEncryptionIsNotSupported();
            }

            byte[] messageBytes = isEncrypted
                ? _messageCodec.EncodeEncryptedMessage(message, _config.AuthKey, _config.Salt, _config.SessionId, Sender.Client)
                : _messageCodec.EncodePlainMessage(message);

            return messageBytes;
        }

        [DebuggerStepThrough]
        protected void ThrowIfDiconnected()
        {
            if (!_transport.IsConnected)
            {
                throw new InvalidOperationException("Not allowed when disconnected.");
            }
        }

        [DebuggerStepThrough]
        protected void ThrowIfEncryptionIsNotSupported()
        {
            if (!IsEncryptionSupported)
            {
                throw new InvalidOperationException("Encryption is not supported. Setup encryption first by calling Configure() method.");
            }
        }

        #region Disposable

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (isDisposing)
            {
                _transport.Dispose();
                _transport = null;
            }
        }

        #endregion
    }
}
