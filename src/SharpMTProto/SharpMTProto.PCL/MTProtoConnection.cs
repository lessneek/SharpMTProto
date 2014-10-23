// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel;
using Catel.Logging;
using Catel.Reflection;
using MTProtoSchema;
using Nito.AsyncEx;
using SharpMTProto.Annotations;
using SharpMTProto.Messages;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;
using AsyncLock = Nito.AsyncEx.AsyncLock;

namespace SharpMTProto
{
    public class OutboxRelay
    {
        private readonly AsyncProducerConsumerQueue<MessageSending> _queueToSend = new AsyncProducerConsumerQueue<MessageSending>();

        public void EnqueueToSend(MessageSending messageSending)
        {
            _queueToSend.Enqueue(messageSending);
        }
    }

    /// <summary>
    ///     Interface of MTProto connection.
    /// </summary>
    public interface IMTProtoConnection : ITLAsyncMethods, IDisposable
    {
        /// <summary>
        ///     In messages history.
        /// </summary>
        IObservable<IMessage> InMessagesHistory { get; }

        /// <summary>
        ///     Out messages history.
        /// </summary>
        IObservable<IMessage> OutMessagesHistory { get; }

        /// <summary>
        ///     A state.
        /// </summary>
        MTProtoConnectionState State { get; }

        /// <summary>
        ///     Is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        ///     Default RPC timeout.
        /// </summary>
        TimeSpan DefaultRpcTimeout { get; set; }

        /// <summary>
        ///     Default connect timeout.
        /// </summary>
        TimeSpan DefaultConnectTimeout { get; set; }

        /// <summary>
        ///     Is encryption supported.
        /// </summary>
        bool IsEncryptionSupported { get; }

        /// <summary>
        ///     Configure connection.
        /// </summary>
        /// <param name="config">Connection configuration.</param>
        void Configure(ConnectionConfig config);

        /// <summary>
        ///     Diconnect.
        /// </summary>
        Task Disconnect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect(CancellationToken cancellationToken);

        Task SendMessageAsync(IMessage message, bool isEncrypted);
        Task SendMessageAsync(IMessage message, bool isEncrypted, CancellationToken cancellationToken);
        Task<object> RpcAsync(object request);
        Task<object> RpcAsync(object request, TimeSpan timeout);
        Task<object> RpcAsync(object request, CancellationToken cancellationToken);
        Task<object> RpcAsync(object request, TimeSpan timeout, CancellationToken cancellationToken);

        Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags);
        Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, TimeSpan timeout);
        Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, CancellationToken cancellationToken);

        Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken,
            Func<TResponse, bool> predicate = null);
    }

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

        #region Messages hubs.
        private readonly Subject<object> _responses = new Subject<object>();
        private Subject<IMessage> _inMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _inMessagesHistory = new ReplaySubject<IMessage>(100);
        private AsyncSubject<IMessage> _outMessages = new AsyncSubject<IMessage>();
        private ReplaySubject<IMessage> _outMessagesHistory = new ReplaySubject<IMessage>(100);
        #endregion

        private readonly AsyncLock _connectionLock = new AsyncLock();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IMessageCodec _messageCodec;
        private readonly Dictionary<int, Rpc> _rpcCalls = new Dictionary<int, Rpc>();
        private readonly ITransport _transport;
        private ConnectionConfig _config;
        private CancellationToken _connectionCancellationToken;
        private CancellationTokenSource _connectionCts;
        private bool _isDisposed;
        private uint _messageSeqNumber;
        private volatile int _rpcCall;
        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;

        public MTProtoConnection([NotNull] TransportConfig transportConfig, [NotNull] ITransportFactory transportFactory, [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator, [NotNull] IMessageCodec messageCodec)
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

            tlRig.PrepareSerializersForAllTLObjectsInAssembly(typeof (ITLAsyncMethods).GetAssemblyEx());

            // Init transport.
            _transport = transportFactory.CreateTransport(transportConfig);

            // History of messages in/out.
            _inMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_inMessagesHistory);
            _outMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_outMessagesHistory);

            // Connector in/out.
            _transport.ObserveOn(DefaultScheduler.Instance).Do(bytes => LogMessageInOut(bytes, "IN")).Subscribe(ProcessIncomingMessageBytes);
//            _outMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(ProcessOutMessage);

            _inMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(ProcessIncomingMessage);
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

        public IObservable<IMessage> InMessagesHistory
        {
            get { return _inMessagesHistory; }
        }

        public IObservable<IMessage> OutMessagesHistory
        {
            get { return _outMessagesHistory; }
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

            await Task.Run(async () =>
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
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task Disconnect()
        {
            await Task.Run(async () =>
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
            }, CancellationToken.None).ConfigureAwait(false);
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

        public async Task<object> RpcAsync(object request)
        {
            return await RpcAsync(request, DefaultRpcTimeout, CancellationToken.None);
        }

        public async Task<object> RpcAsync(object request, TimeSpan timeout)
        {
            return await RpcAsync(request, timeout, CancellationToken.None);
        }

        public async Task<object> RpcAsync(object request, CancellationToken cancellationToken)
        {
            return await RpcAsync(request, DefaultRpcTimeout, cancellationToken);
        }

        public async Task<object> RpcAsync(object request, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Argument.IsNotNull(() => request);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();
            ThrowIfEncryptionIsNotSupported();

            Rpc rpc = CreateRpc(request);

            MessageSending messageSending = CreateNextMessageSending(request, MessageSendingFlags.EncryptedAndContentRelated);
            IRpcResult result =
                await
                    SendMessageAsync<IRpcResult>(messageSending, MessageSendingFlags.EncryptedAndContentRelated, timeout, cancellationToken,
                        r => r.ReqMsgId == messageSending.Message.MsgId);
            return result.Result;
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags)
        {
            return await SendRequestAsync<TResponse>(request, flags, DefaultRpcTimeout, CancellationToken.None);
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, TimeSpan timeout)
        {
            return await SendRequestAsync<TResponse>(request, flags, timeout, CancellationToken.None);
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return await SendRequestAsync<TResponse>(request, flags, DefaultRpcTimeout, cancellationToken);
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(object request, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken,
            Func<TResponse, bool> predicate = null)
        {
            Argument.IsNotNull(() => request);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();

            MessageSending messageSending = CreateNextMessageSending(request, flags);
            return await SendMessageAsync(messageSending, flags, timeout, cancellationToken, predicate);
        }

        public async Task SendMessageAsync(IMessage message, bool isEncrypted)
        {
            await SendMessageAsync(message, isEncrypted, CancellationToken.None);
        }

        public async Task SendMessageAsync(IMessage message, bool isEncrypted, CancellationToken cancellationToken)
        {
            Argument.IsNotNull(() => message);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCancellationToken))
            {
                await SendAsync(WrapMessage(message, isEncrypted), cts.Token);
            }
        }

        private Rpc CreateRpc(object request)
        {
            MessageSending messageSending = CreateNextMessageSending(request, MessageSendingFlags.EncryptedAndContentRelated);
            lock (_rpcCalls)
            {
                var rpc = new Rpc(++_rpcCall, messageSending);
                _rpcCalls.Add(rpc.Id, rpc);
                return rpc;
            }
        }

        private async Task<TResponse> SendMessageAsync<TResponse>(MessageSending messageSending, MessageSendingFlags flags, TimeSpan timeout,
            CancellationToken cancellationToken, Func<TResponse, bool> predicate = null)
        {
            Argument.IsNotNull(() => messageSending);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();

            //return await SendRequestAsync(WrapMessage(message, isEncrypted), timeout, cancellationToken, predicate);

            byte[] messageBytes = WrapMessage(messageSending.Message, flags.HasFlag(MessageSendingFlags.Encrypted));

            predicate = predicate ?? (response => true);

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCancellationToken))
            {
                Task<TResponse> resultTask =
                    _responses.Where(o => o is TResponse).Select(o => (TResponse) o).Where(predicate).FirstAsync().Timeout(timeout).ToTask(cts.Token);
                await SendAsync(messageBytes, cts.Token);
                return await resultTask;
            }
        }

        private async Task<TResponse> PlainRpcAsync<TResponse>(object request)
        {
            return await PlainRpcAsync<TResponse>(request, DefaultRpcTimeout);
        }

        private async Task<TResponse> PlainRpcAsync<TResponse>(object request, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                return await SendRequestAsync<TResponse>(request, MessageSendingFlags.None, cts.Token);
            }
        }

        private async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDiconnected();
            LogMessageInOut(data, "OUT");
            await _transport.SendAsync(data, cancellationToken);
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="messageBytes">Incoming bytes.</param>
        private void ProcessIncomingMessageBytes(byte[] messageBytes)
        {
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
                            string.Format("Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.",
                                messageBytes.Length));
                    }
                    authKeyId = streamer.ReadUInt64();
                }

                IMessage message;

                if (authKeyId == 0)
                {
                    // Assume the message bytes has a plain (unencrypted) message.
                    Log.Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is a plain (unencrypted) message.", authKeyId));

                    message = _messageCodec.UnwrapPlainMessage(messageBytes);

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
                    message = _messageCodec.UnwrapEncryptedMessage(messageBytes, _config.AuthKey, Sender.Server, out salt, out sessionId);
                    // TODO: check salt.
                    if (sessionId != _config.SessionId)
                    {
                        throw new InvalidMessageException(string.Format("Invalid session ID {0}. Expected {1}.", sessionId, _config.SessionId));
                    }
                    Log.Debug(string.Format("Received encrypted message. Message ID = 0x{0:X16}.", message.MsgId));
                }

                _inMessages.OnNext(message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to receive a message.");
            }
        }

        private async void ProcessIncomingMessage(IMessage message)
        {
            try
            {
                object msgBody = message.Body;

                Log.Debug("Incoming message data of type = {0}.", msgBody.GetType());

                var body = msgBody as IBadMsgNotification;
                if (body != null)
                {
                    await ProcessBadMsgNotificationAsync(body);
                }
                else if (msgBody is IMessageContainer)
                {
                    #region Description
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

                    var msgContainer = msgBody as MsgContainer;
                    if (msgContainer != null)
                    {
                        if (msgContainer.Messages.Any(msg => msg.MsgId >= message.MsgId || msg.Seqno >= message.Seqno))
                        {
                            throw new InvalidMessageException("Container MessageId must be greater than all MsgIds of inner messages.");
                        }
                        foreach (Message msg in msgContainer.Messages)
                        {
                            ProcessIncomingMessage(msg);
                        }
                    }
                    else
                    {
                        Log.Debug("Unsupported message container of type: {0}.", msgBody.GetType());
                    }
                }
                else
                {
                    _responses.OnNext(msgBody);
                }
            }
            catch (Exception e)
            {
                Log.Debug(e, "Error while processing incoming message.");
            }
        }

        /// <summary>
        ///     Process bad message notification.
        /// </summary>
        /// <param name="notification">Notification.</param>
        private async Task ProcessBadMsgNotificationAsync(IBadMsgNotification notification)
        {
            #region Notice of Ignored Error Message
            /* In certain cases, a server may notify a client that its incoming message was ignored for whatever reason.
             * Note that such a notification cannot be generated unless a message is correctly decoded by the server.
             * 
             * bad_msg_notification#a7eff811 bad_msg_id:long bad_msg_seqno:int error_code:int = BadMsgNotification;
             * bad_server_salt#edab447b bad_msg_id:long bad_msg_seqno:int error_code:int new_server_salt:long = BadMsgNotification;
             * 
             * Here, error_code can also take on the following values:
             * 
             * 16: msg_id too low (most likely, client time is wrong; it would be worthwhile to synchronize it using msg_id notifications
             *     and re-send the original message with the “correct” msg_id or wrap it in a container with a new msg_id if the original
             *     message had waited too long on the client to be transmitted)
             * 17: msg_id too high (similar to the previous case, the client time has to be synchronized, and the message re-sent with the correct msg_id)
             * 18: incorrect two lower order msg_id bits (the server expects client message msg_id to be divisible by 4)
             * 19: container msg_id is the same as msg_id of a previously received message (this must never happen)
             * 20: message too old, and it cannot be verified whether the server has received a message with this msg_id or not
             * 32: msg_seqno too low (the server has already received a message with a lower msg_id but with either a higher or an equal and odd seqno)
             * 33: msg_seqno too high (similarly, there is a message with a higher msg_id but with either a lower or an equal and odd seqno)
             * 34: an even msg_seqno expected (irrelevant message), but odd received
             * 35: odd msg_seqno expected (relevant message), but even received
             * 48: incorrect server salt (in this case, the bad_server_salt response is received with the correct salt, and the message is to be re-sent with it)
             * 64: invalid container.
             * The intention is that error_code values are grouped (error_code >> 4): for example, the codes 0x40 - 0x4f correspond to errors in container decomposition.
             * 
             * Notifications of an ignored message do not require acknowledgment (i.e., are irrelevant).
             * 
             * Important: if server_salt has changed on the server or if client time is incorrect, any query will result in a notification in the above format.
             * The client must check that it has, in fact, recently sent a message with the specified msg_id, and if that is the case,
             * update its time correction value (the difference between the client’s and the server’s clocks) and the server salt based on msg_id
             * and the server_salt notification, so as to use these to (re)send future messages.
             * In the meantime, the original message (the one that caused the error message to be returned) must also be re-sent with a better msg_id and/or server_salt.
             * 
             * In addition, the client can update the server_salt value used to send messages to the server,
             * based on the values of RPC responses or containers carrying an RPC response,
             * provided that this RPC response is actually a match for the query sent recently.
             * (If there is doubt, it is best not to update since there is risk of a replay attack).
             * 
             * https://core.telegram.org/mtproto/service_messages_about_messages#notice-of-ignored-error-message
             */
            #endregion

            var badServerSalt = notification as BadServerSalt;
            if (badServerSalt != null)
            {
                var errorCode = (ErrorCode) badServerSalt.ErrorCode;
                Debug.Assert(errorCode == ErrorCode.IncorrectServerSalt);

                Log.Debug(string.Format("Bad server salt in message (MessageId = 0x{0:X}, Seqno = {1}). Error code = {2}.", badServerSalt.BadMsgId,
                    badServerSalt.BadMsgSeqno, errorCode));

                Log.Debug("Searching for bad message in out history...");

                IMessage badMessage = await _outMessagesHistory.FirstOrDefaultAsync(m => m.MsgId == badServerSalt.BadMsgId);
                if (badMessage == null || badMessage.Seqno != badServerSalt.BadMsgSeqno)
                {
                    Log.Info("Bad message not found in the out history.");
                    return;
                }

                Log.Debug("Message found. Setting new salt.");

                _config.Salt = badServerSalt.NewServerSalt;

                Log.Debug("Resending bad message with the new salt.");

                throw new NotImplementedException();
                //EncryptedMessage(badMessage.Body);
            }

            var badMsgNotification = notification as BadMsgNotification;
            if (badMsgNotification != null)
            {
                var errorCode = (ErrorCode) badMsgNotification.ErrorCode;
                switch (errorCode)
                {
                    case ErrorCode.MsgIdIsTooSmall:
                        break;
                    case ErrorCode.MsgIdIsTooBig:
                        break;
                    case ErrorCode.MsgIdBadTwoLowBytes:
                        break;
                    case ErrorCode.MsgIdDuplicate:
                        break;
                    case ErrorCode.MsgTooOld:
                        break;
                    case ErrorCode.MsgSeqnoIsTooLow:
                        break;
                    case ErrorCode.MsgSeqnoIsTooBig:
                        break;
                    case ErrorCode.MsgSeqnoNotEven:
                        break;
                    case ErrorCode.MsgSeqnoNotOdd:
                        break;
                    case ErrorCode.IncorrectServerSalt:
                        break;
                    case ErrorCode.InvalidContainer:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
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

        private byte[] WrapMessage(IMessage message, bool isEncrypted)
        {
            if (isEncrypted)
            {
                ThrowIfEncryptionIsNotSupported();
            }

            byte[] messageBytes = isEncrypted
                ? _messageCodec.WrapEncryptedMessage(message, _config.AuthKey, _config.Salt, _config.SessionId, Sender.Client)
                : _messageCodec.WrapPlainMessage(message);

            return messageBytes;
        }

        private MessageSending CreateNextMessageSending(object body, MessageSendingFlags flags)
        {
            return new MessageSending(new Message(GetNextMsgId(), GetNextMsgSeqno(flags.HasFlag(MessageSendingFlags.ContentRelated)), body), flags);
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

        #region TL methods
        /// <summary>
        ///     Request pq.
        /// </summary>
        /// <returns>Response with pq.</returns>
        public async Task<IResPQ> ReqPqAsync(ReqPqArgs args)
        {
            return await PlainRpcAsync<IResPQ>(args);
        }

        public async Task<IServerDHParams> ReqDHParamsAsync(ReqDHParamsArgs args)
        {
            return await PlainRpcAsync<IServerDHParams>(args);
        }

        public async Task<ISetClientDHParamsAnswer> SetClientDHParamsAsync(SetClientDHParamsArgs args)
        {
            return await PlainRpcAsync<ISetClientDHParamsAnswer>(args);
        }

        public async Task<IRpcDropAnswer> RpcDropAnswerAsync(RpcDropAnswerArgs args)
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

                _outMessages.Dispose();
                _outMessages = null;

                _inMessages.Dispose();
                _inMessages = null;

                _inMessagesHistory.Dispose();
                _inMessagesHistory = null;

                _outMessagesHistory.Dispose();
                _outMessagesHistory = null;
            }
        }
        #endregion
    }
}
