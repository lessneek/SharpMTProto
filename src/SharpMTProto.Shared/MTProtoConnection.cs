// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel;
using Catel.Logging;
using Catel.Reflection;
using SharpMTProto.Annotations;
using SharpMTProto.Messaging;
using SharpMTProto.Messaging.Handlers;
using SharpMTProto.Schema;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;
using AsyncLock = Nito.AsyncEx.AsyncLock;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto connection state.
    /// </summary>
    public enum MTProtoConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2
    }

    /// <summary>
    ///     MTProto connect result.
    /// </summary>
    public enum MTProtoConnectResult
    {
        Success,
        Timeout,
        Other
    }

    /// <summary>
    ///     MTProto connection.
    /// </summary>
    public class MTProtoConnection : IMTProtoConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Rnd = new Random();

        private readonly AsyncLock _connectionLock = new AsyncLock();
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly RequestsManager _requestsManager = new RequestsManager();
        private readonly IResponseDispatcher _responseDispatcher = new ResponseDispatcher();

        private readonly ITransport _transport;
        private ConnectionConfig _config;
        private CancellationToken _connectionCancellationToken;
        private CancellationTokenSource _connectionCts;
        private bool _isDisposed;
        private uint _messageSeqNumber;

        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;


        public MTProtoConnection(
            [NotNull] TransportConfig transportConfig,
            [NotNull] ITransportFactory transportFactory,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec)
        {
            Argument.IsNotNull(() => transportConfig);
            Argument.IsNotNull(() => transportFactory);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => messageIdGenerator);
            Argument.IsNotNull(() => messageCodec);

            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;

            DefaultRpcTimeout = Defaults.RpcTimeout;
            DefaultConnectTimeout = Defaults.ConnectTimeout;

            InitTLRig(tlRig);
            InitResponseDispatcher(_responseDispatcher);

            // Init transport.
            _transport = transportFactory.CreateTransport(transportConfig);

            // Connector in/out.
            _transport.ObserveOn(DefaultScheduler.Instance).Do(bytes => LogMessageInOut(bytes, "IN")).Subscribe(ProcessIncomingMessageBytes);
        }

        public TimeSpan DefaultRpcTimeout { get; set; }
        public TimeSpan DefaultConnectTimeout { get; set; }

        public MTProtoConnectionState State
        {
            get { return _state; }
        }

        public bool IsConnected
        {
            get { return _state == MTProtoConnectionState.Connected; }
        }

        public bool IsEncryptionSupported
        {
            get { return _config.AuthKey != null; }
        }

        /// <summary>
        ///     Start sender and receiver tasks.
        /// </summary>
        public async Task<MTProtoConnectResult> Connect()
        {
            return await Connect(CancellationToken.None);
        }

        /// <summary>
        ///     Connect.
        /// </summary>
        public async Task<MTProtoConnectResult> Connect(CancellationToken cancellationToken)
        {
            var result = MTProtoConnectResult.Other;

            await Task.Run(
                async () =>
                {
                    using (await _connectionLock.LockAsync(cancellationToken))
                    {
                        if (_state == MTProtoConnectionState.Connected)
                        {
                            result = MTProtoConnectResult.Success;
                            return;
                        }
                        Debug.Assert(_state == MTProtoConnectionState.Disconnected);
                        try
                        {
                            _state = MTProtoConnectionState.Connecting;
                            Log.Debug("Connecting...");

                            await _transport.ConnectAsync(cancellationToken).ToObservable().Timeout(DefaultConnectTimeout);

                            _connectionCts = new CancellationTokenSource();
                            _connectionCancellationToken = _connectionCts.Token;

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
                },
                cancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task Disconnect()
        {
            await Task.Run(
                async () =>
                {
                    using (await _connectionLock.LockAsync(CancellationToken.None))
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

                        await _transport.DisconnectAsync(CancellationToken.None);
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        ///     Set config.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">The <paramref name="config" /> is <c>null</c>.</exception>
        public void Configure([NotNull] ConnectionConfig config)
        {
            Argument.IsNotNull(() => config);
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

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags)
        {
            return await RequestAsync<TResponse>(requestBody, flags, DefaultRpcTimeout, CancellationToken.None);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout)
        {
            return await RequestAsync<TResponse>(requestBody, flags, timeout, CancellationToken.None);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return await RequestAsync<TResponse>(requestBody, flags, DefaultRpcTimeout, cancellationToken);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Argument.IsNotNull(() => requestBody);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();

            var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using (
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCancellationTokenSource.Token,
                    cancellationToken,
                    _connectionCancellationToken))
            {
                Request<TResponse> request = CreateRequest<TResponse>(requestBody, flags, cts.Token);
                await request.SendAsync();
                return await request.GetResponseAsync();
            }
        }

        public Task<TResponse> RpcAsync<TResponse>(object requestBody)
        {
            return RequestAsync<TResponse>(requestBody, MessageSendingFlags.EncryptedAndContentRelated);
        }

        private static void InitTLRig(TLRig tlRig)
        {
            tlRig.PrepareSerializersForAllTLObjectsInAssembly(typeof (IMTProtoAsyncMethods).GetAssemblyEx());
        }

        private void InitResponseDispatcher(IResponseDispatcher responseDispatcher)
        {
            responseDispatcher.FallbackHandler = new FirstRequestResponseHandler(_requestsManager);
            responseDispatcher.AddHandler(new BadMsgNotificationHandler(this, _requestsManager));
            responseDispatcher.AddHandler(new MessageContainerHandler(_responseDispatcher));
            responseDispatcher.AddHandler(new RpcResultHandler(_requestsManager));
        }

        private Task SendRequestAsync(IRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(
                async () =>
                {
                    byte[] messageBytes = EncodeMessage(request.Message, request.Flags.HasFlag(MessageSendingFlags.Encrypted));
                    await SendAsync(messageBytes, cancellationToken);
                },
                cancellationToken);
        }

        private Request<TResponse> CreateRequest<TResponse>(object body, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            var request = new Request<TResponse>(
                new Message(GetNextMsgId(), GetNextMsgSeqno(flags.HasFlag(MessageSendingFlags.ContentRelated)), body),
                flags,
                SendRequestAsync,
                cancellationToken);
            _requestsManager.Add(request);
            return request;
        }

        private Task<TResponse> PlainSystemRequestAsync<TResponse>(object requestBody)
        {
            return RequestAsync<TResponse>(requestBody, MessageSendingFlags.None);
        }

        private Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDiconnected();
            LogMessageInOut(data, "OUT");
            return _transport.SendAsync(data, cancellationToken);
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
                        throw new InvalidMessageException(string.Format("Invalid session ID {0}. Expected {1}.", sessionId, _config.SessionId));
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

                await _responseDispatcher.DispatchAsync(message);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Error while processing incoming message.");
            }
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

        private bool IsIncomingMessageIdValid(ulong messageId)
        {
            // TODO: check.
            return true;
        }

        private byte[] EncodeMessage(IMessage message, bool isEncrypted)
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
        private void ThrowIfDiconnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not allowed when disconnected.");
            }
        }

        [DebuggerStepThrough]
        private void ThrowIfEncryptionIsNotSupported()
        {
            if (!IsEncryptionSupported)
            {
                throw new InvalidOperationException("Encryption is not supported. Setup encryption first by calling Configure() method.");
            }
        }

        [DebuggerStepThrough]
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
            }
        }

        #region MTProto methods
        /// <summary>
        ///     Request pq.
        /// </summary>
        /// <returns>Response with pq.</returns>
        public Task<IResPQ> ReqPqAsync(ReqPqArgs args)
        {
            return PlainSystemRequestAsync<IResPQ>(args);
        }

        public Task<IServerDHParams> ReqDHParamsAsync(ReqDHParamsArgs args)
        {
            return PlainSystemRequestAsync<IServerDHParams>(args);
        }

        public Task<ISetClientDHParamsAnswer> SetClientDHParamsAsync(SetClientDHParamsArgs args)
        {
            return PlainSystemRequestAsync<ISetClientDHParamsAnswer>(args);
        }

        public Task<IRpcDropAnswer> RpcDropAnswerAsync(RpcDropAnswerArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IFutureSalts> GetFutureSaltsAsync(GetFutureSaltsArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IPong> PingAsync(PingArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IPong> PingDelayDisconnectAsync(PingDelayDisconnectArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IDestroySessionRes> DestroySessionAsync(DestroySessionArgs args)
        {
            throw new NotImplementedException();
        }

        public Task HttpWaitAsync(HttpWaitArgs args)
        {
            throw new NotImplementedException();
        }
        #endregion

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
                Disconnect().Wait(5000);
            }
        }
        #endregion
    }
}
