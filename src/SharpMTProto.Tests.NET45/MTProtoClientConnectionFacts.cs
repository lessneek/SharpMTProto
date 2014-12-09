// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using Nito.AsyncEx;
using NUnit.Framework;
using SharpMTProto.Messaging;
using SharpMTProto.Schema;
using SharpMTProto.Tests.TestObjects;
using SharpMTProto.Transport;

namespace SharpMTProto.Tests
{
    [TestFixture]
    [Category("Core")]
    public class MTProtoClientConnectionFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);
        }

        [TearDown]
        public void TearDown()
        {
            LogManager.FlushAll();
        }

        [Test]
        public async Task Should_send_Rpc_and_receive_response()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var config = new ConnectionConfig(TestRig.AuthKey, 100500) {SessionId = 2};

            var messageProcessor = serviceLocator.ResolveType<IMessageCodec>();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};
            var rpcResult = new RpcResult {ReqMsgId = TestMessageIdsGenerator.MessageIds[0], Result = expectedResponse};

            byte[] expectedResponseMessageBytes = messageProcessor.EncodeEncryptedMessage(
                new Message(0x0102030405060708, 3, rpcResult),
                config.AuthKey,
                config.Salt,
                config.SessionId.GetValueOrDefault(),
                Sender.Server);

            SetupMockTransportWhichReturnsBytes(serviceLocator, expectedResponseMessageBytes);
            
            var builder = serviceLocator.ResolveType<IMTProtoClientBuilder>();
            using (var connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.Configure(config);
                await connection.ConnectAsync();

                TestResponse response = await connection.RpcAsync<TestResponse>(request);
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.DisconnectAsync();
            }
        }

        [Test]
        public async Task Should_send_encrypted_message_and_wait_for_response()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var config = new ConnectionConfig(TestRig.AuthKey, 100500) {SessionId = 2};

            var messageProcessor = serviceLocator.ResolveType<IMessageCodec>();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};

            byte[] expectedResponseMessageBytes = messageProcessor.EncodeEncryptedMessage(
                new Message(0x0102030405060708, 3, expectedResponse),
                config.AuthKey,
                config.Salt,
                config.SessionId.GetValueOrDefault(),
                Sender.Server);

            SetupMockTransportWhichReturnsBytes(serviceLocator, expectedResponseMessageBytes);

            var builder = serviceLocator.ResolveType<IMTProtoClientBuilder>();
            using (var connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.Configure(config);
                connection.Transport.SendingTimeout = TimeSpan.FromSeconds(5);
                await connection.ConnectAsync();

                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.EncryptedAndContentRelated);
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.DisconnectAsync();
            }
        }

        [Test]
        public async Task Should_send_plain_message_and_wait_for_response()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var messageProcessor = serviceLocator.ResolveType<IMessageCodec>();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};
            var expectedResponseMessage = new Message(0x0102030405060708, 0, expectedResponse);
            byte[] expectedResponseMessageBytes = messageProcessor.EncodePlainMessage(expectedResponseMessage);

            SetupMockTransportWhichReturnsBytes(serviceLocator, expectedResponseMessageBytes);

            var builder = serviceLocator.ResolveType<IMTProtoClientBuilder>();
            using (var connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.Transport.SendingTimeout = TimeSpan.FromSeconds(5);
                await connection.ConnectAsync();

                // Testing sending a plain message.
                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.None);
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.DisconnectAsync();
            }
        }

        [Test]
        public void Should_throw_on_response_timeout()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var mockTransport = CreateMockTransport();

            serviceLocator.RegisterInstance(CreateMockTransportFactory(mockTransport.Object));

            var testAction = new Func<Task>(
                async () =>
                {
                    var builder = serviceLocator.ResolveType<IMTProtoClientBuilder>();
                    using (var connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
                    {
                        await connection.ConnectAsync();
                        await connection.RequestAsync<TestResponse>(new TestRequest(), MessageSendingFlags.None, TimeSpan.FromSeconds(1));
                    }
                });
            testAction.ShouldThrow<TaskCanceledException>();
        }

        private static void SetupMockTransportWhichReturnsBytes(IServiceLocator serviceLocator, byte[] expectedResponseMessageBytes)
        {
            var inConnector = new Subject<byte[]>();
            var mockTransport = CreateMockTransport();

            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            
            mockTransport.Setup(transport => transport.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => inConnector.OnNext(expectedResponseMessageBytes))
                .Returns(() => TaskConstants.Completed);

            serviceLocator.RegisterInstance(CreateMockTransportFactory(mockTransport.Object));
        }

        private static Mock<IClientTransport> CreateMockTransport()
        {
            var mockTransport = new Mock<IClientTransport>();

            mockTransport.Setup(transport => transport.ConnectAsync()).Returns(() => Task.FromResult(TransportConnectResult.Success));

            mockTransport.Setup(transport => transport.IsConnected).Returns(() => true);

            return mockTransport;
        }

        private static IClientTransportFactory CreateMockTransportFactory(IClientTransport clientTransport)
        {
            var mockTransportFactory = new Mock<IClientTransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<IClientTransportConfig>())).Returns(() => clientTransport);
            return mockTransportFactory.Object;
        }
    }
}
