// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpClientTransportFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests.Transport
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;
    using BigMath.Utils;
    using FluentAssertions;
    using Nito.AsyncEx;
    using NUnit.Framework;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Transport;
    using SharpMTProto.Utils;

    [TestFixture]
    [Category("Transport")]
    public class TcpClientTransportFacts
    {
        private const int ReceiveTimeout = 1000000;
        private IBytesOcean _bytesOcean;
        private IPEndPoint _serverEndPoint;
        private Socket _serverSocket;

        [SetUp]
        public void SetUp()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _serverSocket.Listen(1);
            _serverEndPoint = _serverSocket.LocalEndPoint as IPEndPoint;
            _bytesOcean = BytesOcean.WithBuckets(10, MTProtoDefaults.MaximumMessageLength).Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serverSocket != null)
            {
                _serverSocket.Close();
                _serverSocket = null;
            }
            _serverEndPoint = null;
        }

        [Test]
        public async Task Should_connect_and_disconnect_with_according_state_changes()
        {
            ClientTransportState? currentState = null;

            using (TcpClientTransport clientTransport = CreateTcpTransport())
            {
                currentState = clientTransport.State.Value;
                clientTransport.State.Subscribe(state => currentState = state.NewValue);

                currentState.Should().NotBeNull();
                currentState.Should().Be(ClientTransportState.Disconnected);
                clientTransport.State.Value.Should().Be(ClientTransportState.Disconnected);
                clientTransport.IsConnected.Should().BeFalse();

                await clientTransport.ConnectAsync();

                Socket clientSocket = _serverSocket.Accept();

                clientSocket.Should().NotBeNull();
                clientSocket.IsConnected().Should().BeTrue();

                currentState.Should().Be(ClientTransportState.Connected);
                clientTransport.State.Value.Should().Be(ClientTransportState.Connected);
                clientTransport.IsConnected.Should().BeTrue();

                await clientTransport.DisconnectAsync();

                clientSocket.IsConnected().Should().BeFalse();

                currentState.Should().Be(ClientTransportState.Disconnected);
                clientTransport.State.Value.Should().Be(ClientTransportState.Disconnected);
                clientTransport.IsConnected.Should().BeFalse();
            }
        }

        [Test]
        public async Task Should_process_remote_disconnection()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            clientTransport.State.Value.Should().Be(ClientTransportState.Disconnected);

            await clientTransport.ConnectAsync();

            Socket clientSocket = _serverSocket.Accept();

            clientSocket.Should().NotBeNull();
            clientSocket.IsConnected().Should().BeTrue();

            clientTransport.State.Value.Should().Be(ClientTransportState.Connected);

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Disconnect(false);
            clientSocket.Close();

            await Task.Delay(200);

            clientTransport.State.Value.Should().Be(ClientTransportState.Disconnected);

            await clientTransport.DisconnectAsync();

            clientTransport.State.Value.Should().Be(ClientTransportState.Disconnected);
        }

        [Test]
        public async Task Should_receive()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();
            ITransportPacketProcessor packetProcessor = CreatePacketProcessor();

            Task<IBytesBucket> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(ReceiveTimeout)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            var payload = new ArraySegment<byte>("010203040506070809".HexToBytes());
            byte[] packetBytes = packetProcessor.WritePacket(payload);
            clientSocket.Send(packetBytes);

            using (IBytesBucket receivedDataBucket = await receiveTask)
            {
                receivedDataBucket.UsedBytes.Should().Equal(payload);
            }

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_big_payload()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();
            ITransportPacketProcessor packetProcessor = CreatePacketProcessor();

            Task<IBytesBucket> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(ReceiveTimeout)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            var payload = new ArraySegment<byte>(Enumerable.Range(0, 255).Select(i => (byte) i).ToArray());

            byte[] packetBytes = packetProcessor.WritePacket(payload);

            clientSocket.Send(packetBytes);

            using (IBytesBucket receivedData = await receiveTask)
            {
                receivedData.UsedBytes.Should().Equal(payload);
            }

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_multiple_packets()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();
            ITransportPacketProcessor packetProcessor = CreatePacketProcessor();

            var receivedMessages = new AsyncProducerConsumerQueue<IBytesBucket>();
            clientTransport.Subscribe(receivedMessages.Enqueue);

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload1 = Enumerable.Range(0, 10).Select(i => (byte) i).ToArray();
            byte[] packet1Bytes = packetProcessor.WritePacket(payload1);

            byte[] payload2 = Enumerable.Range(11, 40).Select(i => (byte) i).ToArray();
            byte[] packet2Bytes = packetProcessor.WritePacket(payload2);

            byte[] payload3 = Enumerable.Range(51, 205).Select(i => (byte) i).ToArray();
            byte[] packet3Bytes = packetProcessor.WritePacket(payload3);

            byte[] allData = ArrayUtils.Combine(packet1Bytes, packet2Bytes, packet3Bytes);

            byte[] dataPart1;
            byte[] dataPart2;
            allData.Split(50, out dataPart1, out dataPart2);

            clientSocket.Send(dataPart1);
            await Task.Delay(100);

            clientSocket.Send(dataPart2);
            await Task.Delay(100);

            using (IBytesBucket receivedData1 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(ReceiveTimeout).Token))
            {
                receivedData1.UsedBytes.Should().Equal(payload1);
            }

            using (IBytesBucket receivedData2 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(ReceiveTimeout).Token))
            {
                receivedData2.UsedBytes.Should().Equal(payload2);
            }

            using (IBytesBucket receivedData3 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(ReceiveTimeout).Token))
            {
                receivedData3.UsedBytes.Should().Equal(payload3);
            }

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_small_parts_less_than_4_bytes()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();
            ITransportPacketProcessor packetProcessor = CreatePacketProcessor();

            Task<IBytesBucket> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(ReceiveTimeout)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            byte[] packet = packetProcessor.WritePacket(payload);
            byte[] part1 = packet.Take(1).ToArray();
            byte[] part2 = packet.Skip(part1.Length).Take(2).ToArray();
            byte[] part3 = packet.Skip(part1.Length + part2.Length).Take(3).ToArray();
            byte[] part4 = packet.Skip(part1.Length + part2.Length + part3.Length).ToArray();

            clientSocket.Send(part1);
            await Task.Delay(100);
            clientSocket.Send(part2);
            await Task.Delay(200);
            clientSocket.Send(part3);
            await Task.Delay(50);
            clientSocket.Send(part4);
            await Task.Delay(50);

            using (IBytesBucket receivedData = await receiveTask)
            {
                receivedData.UsedBytes.Should().Equal(payload);
            }

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_send()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();
            ITransportPacketProcessor packetProcessor = CreatePacketProcessor();
            var receivedPayloads = new List<IBytesBucket>();
            packetProcessor.IncomingMessageBuckets.Subscribe(bucket => receivedPayloads.Add(bucket));

            await clientTransport.ConnectAsync();

            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            IBytesBucket sendPayloadBucket = await _bytesOcean.TakeAsync(100).ConfigureAwait(false);

            Buffer.BlockCopy(payload, 0, sendPayloadBucket.Bytes.Array, sendPayloadBucket.Bytes.Offset, payload.Length);
            sendPayloadBucket.Used = payload.Length;
            await clientTransport.SendAsync(sendPayloadBucket);

            var receiverBuffer = new byte[100];
            int received = clientSocket.Receive(receiverBuffer);

            await packetProcessor.ProcessIncomingPacketAsync(new ArraySegment<byte>(receiverBuffer, 0, received));
            receivedPayloads.Should().HaveCount(1);
            IBytesBucket receivedPayloadBucket = receivedPayloads.First();
            ArraySegment<byte> receivedPayload = receivedPayloadBucket.UsedBytes;

            receivedPayload.Should().Equal(payload);
        }

        [Test]
        public async Task Should_timeout_on_connect()
        {
            var config = new TcpClientTransportConfig("1.0.0.1", 1) {ConnectTimeout = TimeSpan.FromMilliseconds(100)};
            TcpClientTransport transport = CreateTcpTransport(config);
            TransportConnectResult result = await transport.ConnectAsync();
            result.Should().Be(TransportConnectResult.Timeout);
        }

        #region Helper methods.

        private TcpClientTransport CreateTcpTransport(TcpClientTransportConfig config = null)
        {
            config = config ?? new TcpClientTransportConfig(_serverEndPoint.Address.ToString(), _serverEndPoint.Port) {MaxBufferSize = 0xFF};
            var transport = new TcpClientTransport(config, new TcpTransportFullPacketProcessor(_bytesOcean), _bytesOcean);
            return transport;
        }

        private static ITransportPacketProcessor CreatePacketProcessor()
        {
            return new TcpTransportFullPacketProcessor(BytesOcean.WithBuckets(4, 1024).Build());
        }

        #endregion
    }
}
