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
    using System.Reactive.Subjects;
    using System.Reactive.Threading.Tasks;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using BigMath.Utils;
    using Catel.Logging;
    using Nito.AsyncEx;
    using SharpTL;
    using Utils;

    /// <summary>
    ///     MTProto TCP clientTransport.
    /// </summary>
    public class TcpClientTransport : IClientTransport
    {
        private const int PacketLengthBytesCount = 4;
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly bool _isConnectedSocket;
        private readonly byte[] _readerBuffer;
        private readonly IPEndPoint _remoteEndPoint;

        private readonly AsyncLock _stateAsyncLock = new AsyncLock();
        private readonly byte[] _tempLengthBuffer = new byte[PacketLengthBytesCount];

        private CancellationTokenSource _connectionCancellationTokenSource;
        private Subject<byte[]> _in = new Subject<byte[]>();
        private int _nextPacketBytesCountLeft;
        private byte[] _nextPacketDataBuffer;
        private TLStreamer _nextPacketStreamer;
        private int _packetNumber;
        private Socket _socket;
        private volatile ClientTransportState _state = ClientTransportState.Disconnected;
        private int _tempLengthBufferFill;

        public TcpClientTransport(TcpClientTransportConfig config)
        {
            if (config.Port <= 0 || config.Port > ushort.MaxValue)
            {
                throw new ArgumentException(string.Format("Port {0} is incorrect.", config.Port));
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(config.IPAddress, out ipAddress))
            {
                throw new ArgumentException(string.Format("IP address [{0}] is incorrect.", config.IPAddress));
            }

            _remoteEndPoint = new IPEndPoint(ipAddress, config.Port);
            ConnectTimeout = config.ConnectTimeout;
            SendingTimeout = config.SendingTimeout;

            _readerBuffer = new byte[config.MaxBufferSize];
        }

        /// <summary>
        ///     Create a new instance of <see cref="TcpClientTransport" /> with connected socket.
        /// </summary>
        /// <param name="socket">Connected socket.</param>
        public TcpClientTransport([NotNull] Socket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            _isConnectedSocket = true;
            _socket = socket;

            _remoteEndPoint = _socket.RemoteEndPoint as IPEndPoint;
            _readerBuffer = new byte[_socket.ReceiveBufferSize];

            InternalConnectAsync().Wait();
        }

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            ThrowIfDisposed();
            return _in.Subscribe(observer);
        }

        public bool IsConnected
        {
            get { return State == ClientTransportState.Connected; }
        }

        public ClientTransportState State
        {
            get { return _state; }
        }

        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan SendingTimeout { get; set; }

        public Task<TransportConnectResult> ConnectAsync()
        {
            ThrowIfConnectedSocket();
            return InternalConnectAsync();
        }

        public async Task DisconnectAsync()
        {
            using (await _stateAsyncLock.LockAsync().ConfigureAwait(false))
            {
                Debug.Assert(_state != ClientTransportState.Connecting, "This should never happens.");
                Debug.Assert(_state != ClientTransportState.Disconnecting, "This should never happens.");

                if (_state != ClientTransportState.Connected)
                {
                    Log.Debug(string.Format("Client transport ({0}) could not disconnect in non connected state.", _remoteEndPoint));
                    return;
                }
                _state = ClientTransportState.Disconnecting;
                Log.Debug(string.Format("Client transport ({0}) disconnecting.", _remoteEndPoint));

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
                        if (_socket.IsConnected())
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                            await _socket.DisconnectAsync(awaitable);
                            _socket.Dispose();
                        }
                    }
                    catch (SocketException e)
                    {
                        Log.Debug(e);
                    }
                    finally
                    {
                        _socket = null;
                    }
                }

                _state = ClientTransportState.Disconnected;
                Log.Debug(string.Format("Client transport ({0}) disconnected.", _remoteEndPoint));
            }
        }

        public void Send(byte[] payload)
        {
            SendAsync(payload).Wait();
        }

        public Task SendAsync(byte[] payload)
        {
            return SendAsync(payload, CancellationToken.None);
        }

        public Task SendAsync(byte[] payload, CancellationToken token)
        {
            ThrowIfDisposed();
            return Task.Run(async () =>
            {
                var packet = new TcpTransportPacket(_packetNumber++, payload);

                var args = new SocketAsyncEventArgs();
                args.SetBuffer(packet.Data, 0, packet.Data.Length);

                var awaitable = new SocketAwaitable(args);
                await _socket.SendAsync(awaitable);
                // TODO: add timeout and exception catcher.
            },
                token);
        }

        private async Task<TransportConnectResult> InternalConnectAsync()
        {
            Log.Debug(string.Format("Client transport ({0}) connecting...", _remoteEndPoint));

            ThrowIfDisposed();

            using (await _stateAsyncLock.LockAsync().ConfigureAwait(false))
            {
                Debug.Assert(_state != ClientTransportState.Connecting, "This should never happens.");
                Debug.Assert(_state != ClientTransportState.Disconnecting, "This should never happens.");

                var result = TransportConnectResult.Unknown;

                if (_state == ClientTransportState.Connected)
                {
                    Log.Debug(string.Format("Client transport ({0}) already connected.", _remoteEndPoint));
                    return TransportConnectResult.Success;
                }
                _state = ClientTransportState.Connecting;

                if (_isConnectedSocket)
                {
                    _state = ClientTransportState.Connected;
                    result = TransportConnectResult.Success;
                }
                else
                {
                    using (var args = new SocketAsyncEventArgs {RemoteEndPoint = _remoteEndPoint})
                    {
                        var awaitable = new SocketAwaitable(args);

                        try
                        {
                            _packetNumber = 0;
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
                            Log.Debug(e);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                            _state = ClientTransportState.Disconnected;
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
                        _state = ClientTransportState.Connected;
                        break;
                    case TransportConnectResult.Unknown:
                    case TransportConnectResult.Fail:
                    case TransportConnectResult.Timeout:
                        _socket.Close();
                        _socket = null;
                        _state = ClientTransportState.Disconnected;
                        return result;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _connectionCancellationTokenSource = new CancellationTokenSource();
                StartReceiver(_connectionCancellationTokenSource.Token);

                return result;
            }
        }

        private void StartReceiver(CancellationToken token)
        {
            Func<Task> receiver = async () =>
            {
                Log.Debug(string.Format("Receiver task ({0}) was started.", _remoteEndPoint));
                bool canceled = false;
                try
                {
                    using (var args = new SocketAsyncEventArgs())
                    {
                        args.SetBuffer(_readerBuffer, 0, _readerBuffer.Length);
                        var awaitable = new SocketAwaitable(args);

                        while (!token.IsCancellationRequested && _socket.IsConnected())
                        {
                            try
                            {
                                Log.Debug(string.Format("Awaiting socket ({0}) receive async...", _remoteEndPoint));

                                await _socket.ReceiveAsync(awaitable);

                                Log.Debug(string.Format("Socket ({0}) has received {1} bytes async.", _remoteEndPoint, args.BytesTransferred));
                            }
                            catch (SocketException e)
                            {
                                Log.Debug(e);
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

                            try
                            {
                                await ProcessReceivedDataAsync(new ArraySegment<byte>(_readerBuffer, 0, bytesRead));
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Critical error while precessing received data.");
                                break;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    canceled = true;
                }
                catch (Exception e)
                {
                    Log.Debug(e);
                }

                if (_state == ClientTransportState.Connected)
                {
                    await DisconnectAsync();
                }

                Log.Debug(string.Format("Receiver task ({0}) was {1}.", _remoteEndPoint, canceled ? "canceled" : "ended"));
            };

            Task.Run(receiver, token);
        }

        private async Task ProcessReceivedDataAsync(ArraySegment<byte> buffer)
        {
            try
            {
                int bytesRead = 0;
                while (bytesRead < buffer.Count)
                {
                    int startIndex = buffer.Offset + bytesRead;
                    int bytesToRead = buffer.Count - bytesRead;

                    if (_nextPacketBytesCountLeft == 0)
                    {
                        int tempLengthBytesToRead = PacketLengthBytesCount - _tempLengthBufferFill;
                        tempLengthBytesToRead = (bytesToRead < tempLengthBytesToRead) ? bytesToRead : tempLengthBytesToRead;
                        Buffer.BlockCopy(buffer.Array, startIndex, _tempLengthBuffer, _tempLengthBufferFill, tempLengthBytesToRead);

                        _tempLengthBufferFill += tempLengthBytesToRead;
                        if (_tempLengthBufferFill < PacketLengthBytesCount)
                        {
                            break;
                        }

                        startIndex += tempLengthBytesToRead;
                        bytesToRead -= tempLengthBytesToRead;

                        _tempLengthBufferFill = 0;
                        _nextPacketBytesCountLeft = _tempLengthBuffer.ToInt32();

                        if (_nextPacketDataBuffer == null || _nextPacketDataBuffer.Length < _nextPacketBytesCountLeft ||
                            _nextPacketStreamer == null)
                        {
                            _nextPacketDataBuffer = new byte[_nextPacketBytesCountLeft];
                            _nextPacketStreamer = new TLStreamer(_nextPacketDataBuffer);
                        }

                        // Writing packet length.
                        _nextPacketStreamer.Write(_tempLengthBuffer);
                        _nextPacketBytesCountLeft -= PacketLengthBytesCount;
                        bytesRead += PacketLengthBytesCount;
                    }

                    bytesToRead = bytesToRead > _nextPacketBytesCountLeft ? _nextPacketBytesCountLeft : bytesToRead;

                    _nextPacketStreamer.Write(buffer.Array, startIndex, bytesToRead);

                    bytesRead += bytesToRead;
                    _nextPacketBytesCountLeft -= bytesToRead;

                    if (_nextPacketBytesCountLeft > 0)
                    {
                        break;
                    }

                    var packet = new TcpTransportPacket(_nextPacketDataBuffer, 0, (int) _nextPacketStreamer.Position);

                    await ProcessReceivedPacket(packet);

                    _nextPacketBytesCountLeft = 0;
                    _nextPacketStreamer.Position = 0;
                }
            }
            catch (Exception)
            {
                if (_nextPacketStreamer != null)
                {
                    _nextPacketStreamer.Dispose();
                    _nextPacketStreamer = null;
                }
                _nextPacketDataBuffer = null;
                _nextPacketBytesCountLeft = 0;

                throw;
            }
        }

        private Task ProcessReceivedPacket(TcpTransportPacket packet)
        {
            return Task.Run(() => _in.OnNext(packet.GetPayloadCopy()));
        }

        private void ThrowIfConnectedSocket()
        {
            if (_isConnectedSocket)
            {
                throw new NotSupportedException("Not supported in connected socket mode.");
            }
        }

        #region Disposing

        private volatile bool _isDisposed;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual async void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (!isDisposing)
            {
                return;
            }

            if (_state != ClientTransportState.Disconnected)
            {
                await DisconnectAsync();
            }

            if (_nextPacketStreamer != null)
            {
                _nextPacketStreamer.Dispose();
                _nextPacketStreamer = null;
            }
            if (_in != null)
            {
                _in.OnCompleted();
                _in.Dispose();
                _in = null;
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

        #endregion
    }
}
