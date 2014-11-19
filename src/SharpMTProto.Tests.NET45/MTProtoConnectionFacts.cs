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
    public class MTProtoConnectionFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);
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
                config.SessionId,
                Sender.Server);

            SetupMockTransportWhichReturnsBytes(serviceLocator, expectedResponseMessageBytes);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.Configure(config);
                await connection.Connect();

                TestResponse response = await connection.RpcAsync<TestResponse>(request);
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.Disconnect();
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
                config.SessionId,
                Sender.Server);

            SetupMockTransportWhichReturnsBytes(serviceLocator, expectedResponseMessageBytes);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.Configure(config);
                await connection.Connect();

                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.EncryptedAndContentRelated, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.Disconnect();
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

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending a plain message.
                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.None, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.Should().Be(expectedResponse);

                await connection.Disconnect();
            }
        }

        [Test]
        public void Should_throw_on_response_timeout()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var mockTransport = new Mock<ITransport>();

            serviceLocator.RegisterInstance(CreateMockTransportFactory(mockTransport.Object));

            var testAction = new Func<Task>(
                async () =>
                {
                    using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
                    {
                        await connection.Connect();
                        await connection.RequestAsync<TestResponse>(new TestRequest(), MessageSendingFlags.None, TimeSpan.FromSeconds(1));
                    }
                });
            testAction.ShouldThrow<TaskCanceledException>();
        }

        [Test]
        public async Task Should_timeout_on_connect()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.ConnectAsync(It.IsAny<CancellationToken>())).Returns(() => Task.Delay(1000));

            serviceLocator.RegisterInstance(CreateMockTransportFactory(mockTransport.Object));

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.DefaultConnectTimeout = TimeSpan.FromMilliseconds(100);
                MTProtoConnectResult result = await connection.Connect();
                result.Should().Be(MTProtoConnectResult.Timeout);
            }
        }

        private static void SetupMockTransportWhichReturnsBytes(IServiceLocator serviceLocator, byte[] expectedResponseMessageBytes)
        {
            var inConnector = new Subject<byte[]>();
            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(transport => transport.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => inConnector.OnNext(expectedResponseMessageBytes))
                .Returns(() => TaskConstants.Completed);

            serviceLocator.RegisterInstance(CreateMockTransportFactory(mockTransport.Object));
        }

        private static ITransportFactory CreateMockTransportFactory(ITransport transport)
        {
            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<ITransportConfig>())).Returns(() => transport);
            return mockTransportFactory.Object;
        }
    }
}
