// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransportFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.Logging;
using FluentAssertions;
using Nito.AsyncEx;
using NUnit.Framework;
using SharpMTProto.Transport;
using SharpMTProto.Utils;

namespace SharpMTProto.Tests.Transport
{
    [TestFixture]
    [Category("Transport")]
    public class TcpTransportFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _serverSocket.Listen(1);
            _serverEndPoint = _serverSocket.LocalEndPoint as IPEndPoint;
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

        private Socket _serverSocket;
        private IPEndPoint _serverEndPoint;

        private TcpClientTransport CreateTcpTransport()
        {
            var config = new TcpClientTransportConfig(_serverEndPoint.Address.ToString(), _serverEndPoint.Port) {MaxBufferSize = 0xFF};
            var transport = new TcpClientTransport(config);
            return transport;
        }

        [Test]
        public async Task Should_connect_and_disconnect()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            clientTransport.State.Should().Be(TransportState.Disconnected);
            clientTransport.IsConnected.Should().BeFalse();

            await clientTransport.ConnectAsync();

            Socket clientSocket = _serverSocket.Accept();

            clientSocket.Should().NotBeNull();
            clientSocket.IsConnected().Should().BeTrue();

            clientTransport.State.Should().Be(TransportState.Connected);
            clientTransport.IsConnected.Should().BeTrue();

            await clientTransport.DisconnectAsync();
            clientTransport.Dispose();

            clientSocket.IsConnected().Should().BeFalse();

            clientTransport.State.Should().Be(TransportState.Disconnected);
            clientTransport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task Should_process_lost_connection()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            clientTransport.State.Should().Be(TransportState.Disconnected);

            await clientTransport.ConnectAsync();

            Socket clientSocket = _serverSocket.Accept();

            clientSocket.Should().NotBeNull();
            clientSocket.IsConnected().Should().BeTrue();

            clientTransport.State.Should().Be(TransportState.Connected);

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Disconnect(false);
            clientSocket.Close();

            await Task.Delay(200);

            clientTransport.State.Should().Be(TransportState.Disconnected);

            await clientTransport.DisconnectAsync();

            clientTransport.State.Should().Be(TransportState.Disconnected);
        }

        [Test]
        public async Task Should_receive()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            Task<byte[]> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(1000)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            var packet = new TcpTransportPacket(0x0FA0B1C2, payload);
            clientSocket.Send(packet.Data);

            byte[] receivedData = await receiveTask;
            receivedData.Should().Equal(payload);

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_big_payload()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            Task<byte[]> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(1000)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();

            var packet = new TcpTransportPacket(0x0FA0B1C2, payload);
            clientSocket.Send(packet.Data);

            byte[] receivedData = await receiveTask;
            receivedData.Should().Equal(payload);

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_multiple_packets()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            var receivedMessages = new AsyncProducerConsumerQueue<byte[]>();
            clientTransport.Subscribe(receivedMessages.Enqueue);

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload1 = Enumerable.Range(0, 10).Select(i => (byte) i).ToArray();
            var packet1 = new TcpTransportPacket(0, payload1);

            byte[] payload2 = Enumerable.Range(11, 40).Select(i => (byte) i).ToArray();
            var packet2 = new TcpTransportPacket(1, payload2);

            byte[] payload3 = Enumerable.Range(51, 205).Select(i => (byte) i).ToArray();
            var packet3 = new TcpTransportPacket(2, payload3);

            byte[] allData = ArrayUtils.Combine(packet1.Data, packet2.Data, packet3.Data);

            byte[] dataPart1;
            byte[] dataPart2;
            allData.Split(50, out dataPart1, out dataPart2);

            clientSocket.Send(dataPart1);
            await Task.Delay(100);
            
            clientSocket.Send(dataPart2);
            await Task.Delay(100);

            byte[] receivedData1 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData1.Should().Equal(payload1);

            byte[] receivedData2 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData2.Should().Equal(payload2);

            byte[] receivedData3 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData3.Should().Equal(payload3);

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_small_parts_less_than_4_bytes()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            Task<byte[]> receiveTask = clientTransport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(3000)).ToTask();

            await clientTransport.ConnectAsync();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            var packet = new TcpTransportPacket(0x0ABBCCDD, payload);
            byte[] part1 = packet.Data.Take(1).ToArray();
            byte[] part2 = packet.Data.Skip(part1.Length).Take(2).ToArray();
            byte[] part3 = packet.Data.Skip(part1.Length + part2.Length).Take(3).ToArray();
            byte[] part4 = packet.Data.Skip(part1.Length + part2.Length + part3.Length).ToArray();
            
            clientSocket.Send(part1);
            await Task.Delay(100);
            clientSocket.Send(part2);
            await Task.Delay(200);
            clientSocket.Send(part3);
            await Task.Delay(50);
            clientSocket.Send(part4);
            await Task.Delay(50);

            byte[] receivedData = await receiveTask;
            receivedData.Should().Equal(payload);

            await clientTransport.DisconnectAsync();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_send()
        {
            TcpClientTransport clientTransport = CreateTcpTransport();

            await clientTransport.ConnectAsync();
            
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            await clientTransport.SendAsync(payload);

            var buffer = new byte[30];
            var received = clientSocket.Receive(buffer);

            var packet = new TcpTransportPacket(buffer, 0, received);
            var receivedPayload = packet.GetPayloadCopy();

            receivedPayload.ShouldAllBeEquivalentTo(payload);
        }
    }
}
