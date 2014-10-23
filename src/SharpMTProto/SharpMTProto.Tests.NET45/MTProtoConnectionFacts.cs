// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using MTProtoSchema;
using NUnit.Framework;
using SharpMTProto.Messages;
using SharpMTProto.Transport;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoConnectionFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(false);
        }

        [Test]
        public async Task Should_send_and_receive_plain_message()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            byte[] messageData = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();
            byte[] expectedMessageBytes = "00000000000000000807060504030201080100009EB6EFEBFEFF0000".HexToBytes().Concat(messageData).Concat("00".HexToBytes()).ToArray();

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending.
                var expectedMessage = new Message(0x0102030405060708UL, 0, messageData);
                await connection.SendMessageAsync(expectedMessage, false);

                await Task.Delay(100); // Wait while internal sender processes the message.
                mockTransport.Verify(transport => transport.SendAsync(expectedMessageBytes, It.IsAny<CancellationToken>()), Times.Once);

                // Testing receiving.
                mockTransport.Verify(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>()), Times.AtLeastOnce());

                inConnector.OnNext(expectedMessageBytes);

                await Task.Delay(100); // Wait while internal receiver processes the message.
                IMessage actualMessage = await connection.InMessagesHistory.FirstAsync();
                actualMessage.Should().Be(expectedMessage);
                
                await connection.Disconnect();
            }
            mockTransportFactory.Verify();
        }

        [Test]
        public async Task Should_send_encrypted_message_and_wait_for_response()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var config = new ConnectionConfig(TestRig.AuthKey, 100500) {SessionId = 2};

            var messageProcessor = serviceLocator.ResolveType<IMessageCodec>();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};

            byte[] expectedResponseMessageBytes = messageProcessor.EncodeEncryptedMessage(new Message(0x0102030405060708, 3, expectedResponse), config.AuthKey, config.Salt,
                config.SessionId, Sender.Server);

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(transport => transport.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => inConnector.OnNext(expectedResponseMessageBytes))
                .Returns(() => Task.FromResult(false));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.Configure(config);
                await connection.Connect();

                TestResponse response = await connection.SendRequestAsync<TestResponse>(request, MessageSendingFlags.EncryptedAndContentRelated, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.ShouldBeEquivalentTo(expectedResponse);

                await connection.Disconnect();
            }

            mockTransport.Verify();
            mockTransportFactory.Verify();
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

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(transport => transport.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => inConnector.OnNext(expectedResponseMessageBytes))
                .Returns(() => Task.FromResult(false));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending a plain message.
                TestResponse response = await connection.SendRequestAsync<TestResponse>(request, MessageSendingFlags.None, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.ShouldBeEquivalentTo(expectedResponse);

                await connection.Disconnect();
            }
        }

        [Test]
        public void Should_throw_on_response_timeout()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var mockTransport = new Mock<ITransport>();

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);

            var testAction = new Func<Task>(async () =>
            {
                using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
                {
                    await connection.Connect();
                    await connection.SendRequestAsync<TestResponse>(new TestRequest(), MessageSendingFlags.None, TimeSpan.FromSeconds(1));
                }
            });
            testAction.ShouldThrow<TimeoutException>();
        }

        [Test]
        public async Task Should_timeout_on_connect()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.ConnectAsync(It.IsAny<CancellationToken>())).Returns(() => Task.Delay(1000));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.DefaultConnectTimeout = TimeSpan.FromMilliseconds(100);
                MTProtoConnectResult result = await connection.Connect();
                result.ShouldBeEquivalentTo(MTProtoConnectResult.Timeout);
            }
        }
    }
}
