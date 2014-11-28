// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Diagnostics;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
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
    public interface IMTProtoConnection : IDisposable
    {
        /// <summary>
        ///     A state.
        /// </summary>
        MTProtoConnectionState State { get; }

        /// <summary>
        ///     Is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        ///     Is encryption supported.
        /// </summary>
        bool IsEncryptionSupported { get; }

        /// <summary>
        ///     Default connect timeout.
        /// </summary>
        TimeSpan DefaultConnectTimeout { get; set; }

        IMessageDispatcher MessageDispatcher { get; }

        /// <summary>
        ///     Diconnect.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> ConnectAsync();

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
    }

    /// <summary>
    ///     MTProto connection base.
    /// </summary>
    public abstract class MTProtoConnection : IMTProtoConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rnd = new Random();
        private readonly AsyncLock _connectionLock = new AsyncLock();
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageDispatcher _messageDispatcher = new MessageDispatcher();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly TLRig _tlRig;
        private IClientTransport _clientTransport;
        private ConnectionConfig _config = new ConnectionConfig(null, 0);
        private CancellationTokenSource _connectionCts;
        private bool _isDisposed;
        private uint _messageSeqNumber;
        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;

        protected MTProtoConnection([NotNull] IClientTransport clientTransport,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec)
        {
            if (clientTransport == null)
            {
                throw new ArgumentNullException("clientTransport");
            }
            if (tlRig == null)
            {
                throw new ArgumentNullException("tlRig");
            }
            if (messageIdGenerator == null)
            {
                throw new ArgumentNullException("messageIdGenerator");
            }
            if (messageCodec == null)
            {
                throw new ArgumentNullException("messageCodec");
            }

            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;

            DefaultConnectTimeout = Defaults.ConnectTimeout;

            // Init transport.
            _clientTransport = clientTransport;

            // Connector in/out.
            _clientTransport.ObserveOn(DefaultScheduler.Instance)
                .Do(bytes => LogMessageInOut(bytes, "IN"))
                .Subscribe(ProcessIncomingMessageBytes);
        }

        public IMessageDispatcher MessageDispatcher
        {
            get { return _messageDispatcher; }
        }

        protected CancellationTokenSource ConnectionCts
        {
            get { return _connectionCts; }
        }

        public TimeSpan DefaultConnectTimeout { get; set; }

        public bool IsEncryptionSupported
        {
            get { return _config.AuthKey != null; }
        }

        public MTProtoConnectionState State
        {
            get { return _state; }
        }

        public bool IsConnected
        {
            get { return _state == MTProtoConnectionState.Connected; }
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

        /// <summary>
        ///     Connect.
        /// </summary>
        public Task<MTProtoConnectResult> ConnectAsync()
        {
            return Task.Run(async () =>
            {
                var result = MTProtoConnectResult.Other;

                using (await _connectionLock.LockAsync().ConfigureAwait(false))
                {
                    if (_state == MTProtoConnectionState.Connected)
                    {
                        result = MTProtoConnectResult.Success;
                        return result;
                    }
                    Debug.Assert(_state == MTProtoConnectionState.Disconnected);
                    try
                    {
                        _state = MTProtoConnectionState.Connecting;
                        Log.Debug("Connecting...");

                        await _clientTransport.ConnectAsync().ToObservable().Timeout(DefaultConnectTimeout);

                        _connectionCts = new CancellationTokenSource();

                        Log.Debug("Connected.");
                        result = MTProtoConnectResult.Success;
                    }
                    catch (TimeoutException)
                    {
                        result = MTProtoConnectResult.Timeout;
                        Log.Debug(string.Format("Failed to connect due to timeout ({0}s).", DefaultConnectTimeout.TotalSeconds));
                    }
                    catch (Exception e)
                    {
                        result = MTProtoConnectResult.Other;
                        Log.Debug(e, "Failed to connect.");
                    }
                    finally
                    {
                        switch (result)
                        {
                            case MTProtoConnectResult.Success:
                                _state = MTProtoConnectionState.Connected;
                                break;
                            case MTProtoConnectResult.Timeout:
                            case MTProtoConnectResult.Other:
                                _state = MTProtoConnectionState.Disconnected;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
                return result;
            });
        }

        public Task DisconnectAsync()
        {
            return Task.Run(async () =>
            {
                using (await _connectionLock.LockAsync())
                {
                    if (_state == MTProtoConnectionState.Disconnected)
                    {
                        return;
                    }
                    _state = MTProtoConnectionState.Disconnected;

                    if (_connectionCts != null)
                    {
                        _connectionCts.Cancel();
                        _connectionCts.Dispose();
                        _connectionCts = null;
                    }

                    await _clientTransport.DisconnectAsync();
                }
            },
                CancellationToken.None);
        }

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            _tlRig.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        public async Task SendAsync(object requestBody, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken)
        {
            byte[] messageBytes = EncodeMessage(CreateMessage(requestBody, flags.HasFlag(MessageSendingFlags.ContentRelated)),
                flags.HasFlag(MessageSendingFlags.Encrypted));

            var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using (
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token,
                    cancellationToken,
                    _connectionCts.Token))
            {
                await SendAsync(messageBytes, cts.Token);
            }
        }

        protected Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDiconnected();
            LogMessageInOut(data, "OUT");
            return _clientTransport.SendAsync(data, cancellationToken);
        }

        protected Message CreateMessage(object body, bool isContentRelated)
        {
            return new Message(GetNextMsgId(), GetNextMsgSeqno(isContentRelated), body);
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

        private async void ProcessIncomingMessage(IMessage message)
        {
            ThrowIfDisposed();

            try
            {
                Log.Debug("Incoming message data of type = {0}.", message.Body.GetType());

                await _messageDispatcher.DispatchAsync(message);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Error while processing incoming message.");
            }
        }

        private bool IsIncomingMessageIdValid(ulong messageId)
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
            if (!IsConnected)
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

        [DebuggerStepThrough]
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
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
                DisconnectAsync().Wait(5000);
                _clientTransport.Dispose();
                _clientTransport = null;
            }
        }

        #endregion
    }
}
