// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpClientTransport.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Annotations;
    using BigMath.Utils;
    using Dataflows;
    using Nito.AsyncEx;
    using SharpTL;
    using Utils;

    /// <summary>
    ///     MTProto TCP clientTransport.
    /// </summary>
    public class TcpClientTransport : Cancelable, IConnectableClientTransport
    {
        private CancellationTokenSource _connectionCancellationTokenSource;
        private ITransportPacketProcessor _packetProcessor;
        private Socket _socket;
        private readonly IBytesOcean _bytesOcean;
        private readonly TcpClientTransportConfig _config;
        private readonly bool _isConnectedSocket;
        private readonly BufferBlock<IBytesBucket> _outgoingQueue = new BufferBlock<IBytesBucket>();
        private readonly IPEndPoint _remoteEndPoint;
        private readonly AsyncLock _stateAsyncLock = new AsyncLock();
        private ObservableProperty<IClientTransport, ClientTransportState> _state;

        public TcpClientTransport([NotNull] TcpClientTransportConfig config,
            [NotNull] ITransportPacketProcessor packetProcessor,
            IBytesOcean bytesOcean = null)
        {
            if (config.Port <= 0 || config.Port > ushort.MaxValue)
                throw new ArgumentException(string.Format("Port {0} is incorrect.", config.Port));
            if (packetProcessor == null)
                throw new ArgumentNullException("packetProcessor");

            _config = config;
            _packetProcessor = packetProcessor;
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultTransportBytesOcean();
            _state = new ObservableProperty<IClientTransport, ClientTransportState>(this, ClientTransportState.Disconnected);

            TransportId = Guid.NewGuid();

            IPAddress ipAddress;
            if (!IPAddress.TryParse(config.IPAddress, out ipAddress))
            {
                throw new ArgumentException(string.Format("IP address [{0}] is incorrect.", config.IPAddress));
            }
            _remoteEndPoint = new IPEndPoint(ipAddress, config.Port);

            ConnectTimeout = config.ConnectTimeout;
            SendingTimeout = config.SendingTimeout;
        }

        /// <summary>
        ///     Create a new instance of <see cref="TcpClientTransport" /> with connected socket.
        /// </summary>
        /// <param name="socket">Connected socket.</param>
        /// <param name="packetProcessor">Packet processor.</param>
        /// <param name="bytesOcean">Bytes ocean.</param>
        public TcpClientTransport([NotNull] Socket socket, [NotNull] ITransportPacketProcessor packetProcessor, IBytesOcean bytesOcean = null)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (packetProcessor == null)
                throw new ArgumentNullException("packetProcessor");

            _isConnectedSocket = true;
            _socket = socket;
            _packetProcessor = packetProcessor;
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultTransportBytesOcean();

            TransportId = Guid.NewGuid();

            _remoteEndPoint = _socket.RemoteEndPoint as IPEndPoint;
            if (_remoteEndPoint == null)
            {
                throw new TransportException(
                    string.Format(
                        "TcpClientTransport accepts sockets only with RemoteEndPoint of type IPEndPoint, but socket with {0} RemoteEndPoint is found.",
                        _socket.RemoteEndPoint.GetType()));
            }

            _config = new TcpClientTransportConfig(_remoteEndPoint.Address.ToString(), _remoteEndPoint.Port);

            InternalConnectAsync().Wait();
        }

        public IDisposable Subscribe(IObserver<IBytesBucket> observer)
        {
            ThrowIfDisposed();
            return _packetProcessor.IncomingMessageBuckets.Subscribe(observer);
        }

        public Guid TransportId { get; private set; }

        public bool IsConnected
        {
            get { return _state.Value == ClientTransportState.Connected; }
        }

        public IObservableReadonlyProperty<IClientTransport, ClientTransportState> State
        {
            get { return _state.AsReadonly; }
        }

        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan SendingTimeout { get; set; }

        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        public Task<TransportConnectResult> ConnectAsync()
        {
            ThrowIfConnectedSocket();
            return InternalConnectAsync();
        }

        public async Task DisconnectAsync()
        {
            using (await _stateAsyncLock.LockAsync().ConfigureAwait(false))
            {
                Debug.Assert(_state.Value != ClientTransportState.Connecting, "This should never happens.");
                Debug.Assert(_state.Value != ClientTransportState.Disconnecting, "This should never happens.");

                if (_state.Value != ClientTransportState.Connected)
                {
                    LogDebug(string.Format("Could not disconnect in non connected state."));
                    return;
                }
                _state.Value = ClientTransportState.Disconnecting;
                LogDebug(string.Format("Disconnecting."));

                if (_connectionCancellationTokenSource != null)
                {
                    _connectionCancellationTokenSource.Cancel();
                    _connectionCancellationTokenSource.Dispose();
                    _connectionCancellationTokenSource = null;
                }
                await Task.Delay(10);

                using (var args = new SocketAsyncEventArgs {DisconnectReuseSocket = false})
                {
                    var awaitable = new SocketAwaitable(args);
                    try
                    {
                        if (_socket.Connected)
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                            await _socket.DisconnectAsync(awaitable);
                        }
                    }
                    catch (SocketException e)
                    {
                        LogDebug(e);
                    }
                    finally
                    {
                        _socket.Dispose();
                        _socket = null;
                    }
                }

                _state.Value = ClientTransportState.Disconnected;
                LogDebug(string.Format("Disconnected."));
            }
        }

        public Task SendAsync(IBytesBucket payload, CancellationToken cancellationToken)
        {
            if (IsDisposed)
                return TaskConstants.Completed;

            return _outgoingQueue.SendAsync(payload, cancellationToken);
        }

        public Task SendAsync(IBytesBucket payload)
        {
            return SendAsync(payload, CancellationToken.None);
        }

        private async Task<TransportConnectResult> InternalConnectAsync()
        {
            LogDebug(string.Format("Connecting..."));

            ThrowIfDisposed();

            using (await _stateAsyncLock.LockAsync().ConfigureAwait(false))
            {
                Debug.Assert(_state.Value != ClientTransportState.Connecting, "This should never happens.");
                Debug.Assert(_state.Value != ClientTransportState.Disconnecting, "This should never happens.");

                var result = TransportConnectResult.Unknown;

                if (_state.Value == ClientTransportState.Connected)
                {
                    LogDebug(string.Format("Client transport ({0}) already connected.", _remoteEndPoint));
                    return TransportConnectResult.Success;
                }
                _state.Value = ClientTransportState.Connecting;

                if (_isConnectedSocket)
                {
                    _state.Value = ClientTransportState.Connected;
                    result = TransportConnectResult.Success;
                }
                else
                {
                    using (var args = new SocketAsyncEventArgs {RemoteEndPoint = _remoteEndPoint})
                    {
                        var awaitable = new SocketAwaitable(args);

                        try
                        {
                            _socket = new Socket(_remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                            await Task.Run(async () => await _socket.ConnectAsync(awaitable)).ToObservable().Timeout(ConnectTimeout);
                        }
                        catch (TimeoutException)
                        {
                            result = TransportConnectResult.Timeout;
                        }
                        catch (SocketException e)
                        {
                            // Log only. Process actual SocketError below.
                            LogDebug(e);
                        }
                        catch (Exception e)
                        {
                            LogDebug(e, "Fatal error on connect.");
                            _state.Value = ClientTransportState.Disconnected;
                            throw;
                        }

                        if (result == TransportConnectResult.Unknown)
                        {
                            switch (args.SocketError)
                            {
                                case SocketError.Success:
                                case SocketError.IsConnected:
                                    result = TransportConnectResult.Success;
                                    break;
                                case SocketError.TimedOut:
                                    result = TransportConnectResult.Timeout;
                                    break;
                                default:
                                    result = TransportConnectResult.Fail;
                                    break;
                            }
                        }
                    }
                }

                switch (result)
                {
                    case TransportConnectResult.Success:
                        _state.Value = ClientTransportState.Connected;
                        break;
                    case TransportConnectResult.Unknown:
                    case TransportConnectResult.Fail:
                    case TransportConnectResult.Timeout:
                        _socket.Close();
                        _socket = null;
                        _state.Value = ClientTransportState.Disconnected;
                        return result;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _connectionCancellationTokenSource = new CancellationTokenSource();

                StartReceiver(_connectionCancellationTokenSource.Token);
                StartSender(_connectionCancellationTokenSource.Token);

                return result;
            }
        }

        private void StartReceiver(CancellationToken cancellationToken)
        {
            Task.Run(() => ReceiverTask(cancellationToken), cancellationToken);
        }

        private void StartSender(CancellationToken cancellationToken)
        {
            Task.Run(() => SenderTask(cancellationToken), cancellationToken);
        }

        private async Task ReceiverTask(CancellationToken cancellationToken)
        {
            LogDebug(string.Format("Receiver task was started."));
            var canceled = false;

            // TODO: add timeout.
            IBytesBucket receiverBucket = await _bytesOcean.TakeAsync(_config.MaxBufferSize);
            var args = new SocketAsyncEventArgs();
            try
            {
                ArraySegment<byte> bytes = receiverBucket.Bytes;
                args.SetBuffer(bytes.Array, bytes.Offset, bytes.Count);
                var awaitable = new SocketAwaitable(args);

                while (!cancellationToken.IsCancellationRequested && _socket.Connected)
                {
                    try
                    {
                        LogDebug(string.Format("Awaiting socket receive async..."));

                        await _socket.ReceiveAsync(awaitable);

                        LogDebug(string.Format("Socket has received {0} bytes async.", args.BytesTransferred));
                    }
                    catch (SocketException e)
                    {
                        LogDebug(e);
                    }
                    if (args.SocketError != SocketError.Success)
                    {
                        break;
                    }
                    int bytesRead = args.BytesTransferred;
                    if (bytesRead <= 0)
                    {
                        break;
                    }
                    receiverBucket.Used = bytesRead;
                    try
                    {
                        await _packetProcessor.ProcessIncomingPacketAsync(receiverBucket.UsedBytes, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        LogDebug(e, "Critical error while precessing received data.");
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                canceled = true;
            }
            catch (Exception e)
            {
                LogDebug(e);
            }
            finally
            {
                receiverBucket.Dispose();
                args.Dispose();
            }

            if (_state.Value == ClientTransportState.Connected)
            {
                await DisconnectAsync();
            }

            LogDebug(string.Format("Receiver task was {0}.", canceled ? "canceled" : "ended"));
        }

        private async Task SenderTask(CancellationToken token)
        {
            LogDebug(string.Format("Sender task was started."));
            var canceled = false;

            // TODO: add timeout.
            IBytesBucket senderBucket = await _bytesOcean.TakeAsync(_config.MaxBufferSize);
            var senderStreamer = new TLStreamer(senderBucket.Bytes);
            var args = new SocketAsyncEventArgs();
            try
            {
                ArraySegment<byte> bytes = senderBucket.Bytes;
                args.SetBuffer(bytes.Array, bytes.Offset, bytes.Count);
                var awaitable = new SocketAwaitable(args);

                while (!token.IsCancellationRequested && _socket.Connected)
                {
                    senderStreamer.Position = 0;
                    try
                    {
                        LogDebug(string.Format("Awaiting for outgoing queue items..."));

                        using (IBytesBucket payloadBucket = await _outgoingQueue.ReceiveAsync(token).ConfigureAwait(false))
                        {
                            int packetLength = _packetProcessor.WritePacket(payloadBucket.UsedBytes, senderStreamer);
                            args.SetBuffer(bytes.Offset, packetLength);
#if DEBUG
                                var packetBytes = new ArraySegment<byte>(bytes.Array, bytes.Offset, packetLength);
                                LogDebug(string.Format("Sending packet data: {0}.", packetBytes.ToHexString()));
#endif
                        }

                        await _socket.SendAsync(awaitable);

                        LogDebug(string.Format("Socket has sent {0} bytes async.", args.BytesTransferred));
                    }
                    catch (SocketException e)
                    {
                        LogDebug(e);
                    }
                    if (args.SocketError != SocketError.Success)
                    {
                        break;
                    }
                    int bytesRead = args.BytesTransferred;
                    if (bytesRead <= 0)
                    {
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                canceled = true;
            }
            catch (Exception e)
            {
                LogDebug(e);
            }
            finally
            {
                args.Dispose();
                senderStreamer.Dispose();
                senderBucket.Dispose();
            }

            if (_state.Value == ClientTransportState.Connected)
            {
                await DisconnectAsync();
            }

            LogDebug(string.Format("Sender task was {0}.", canceled ? "canceled" : "ended"));
        }

        #region Logging

        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        [Conditional("DEBUG")]
        private void LogDebug(string text)
        {
            Log.Debug(string.Format("[TcpClientTransport] [{0}]: {1}", _remoteEndPoint, text));
        }

        [Conditional("DEBUG")]
        private void LogDebug(Exception exception)
        {
            Log.Debug(exception, string.Format("[TcpClientTransport] [{0}]", _remoteEndPoint));
        }

        [Conditional("DEBUG")]
        private void LogDebug(Exception exception, string message)
        {
            Log.Debug(exception, string.Format("[TcpClientTransport] [{0}]: {1}.", _remoteEndPoint, message));
        }

        #endregion

        private void ThrowIfConnectedSocket()
        {
            if (_isConnectedSocket)
            {
                throw new NotSupportedException("Not supported in connected socket mode.");
            }
        }

        #region Disposing

        protected override async void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_state.Value != ClientTransportState.Disconnected)
                {
                    await DisconnectAsync();
                }
                if (_outgoingQueue != null)
                {
                    _outgoingQueue.Complete();
                }
                if (_packetProcessor != null)
                {
                    _packetProcessor.Dispose();
                    _packetProcessor = null;
                }
                if (_state != null)
                {
                    _state.Dispose();
                    _state = null;
                }
            }
            base.Dispose(isDisposing);
        }

        #endregion
    }
}
