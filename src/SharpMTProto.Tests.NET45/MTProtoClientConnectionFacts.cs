// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests
{
    using System;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using FluentAssertions;
    using Moq;
    using Nito.AsyncEx;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using SharpMTProto.Authentication;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Tests.SetUp;
    using SharpMTProto.Tests.TestObjects;
    using SharpMTProto.Transport;

    [TestFixture]
    [Category("Core")]
    public class MTProtoClientConnectionFacts : SharpMTProtoTestBase
    {
        [Test]
        public async Task Should_send_Rpc_and_receive_response()
        {
            var sessionId = Fixture.Create<ulong>();
            var authInfo = new AuthInfo(AuthKey, Fixture.Create<ulong>());
            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};
            var rpcResult = new RpcResult {ReqMsgId = TestMessageIdsGenerator.MessageIds[0], Result = expectedResponse};

            Override(b =>
            {
                b.Register(context =>
                {
                    var messageCodec = context.Resolve<IMessageCodec>();
                    var authKeysProvider = Resolve<IAuthKeysProvider>();

                    AuthKeyWithId authKeyWithId = authKeysProvider.Add(authInfo.AuthKey);

                    var messageEnvelope = MessageEnvelope.CreateEncrypted(new MTProtoSessionTag(authKeyWithId.AuthKeyId, sessionId),
                        authInfo.Salt,
                        new Message(Fixture.Create<ulong>(), Fixture.Create<uint>(), rpcResult));

                    byte[] expectedResponseMessageBytes = messageCodec.EncodeEncryptedMessage(messageEnvelope,
                        authInfo.AuthKey,
                        MessageCodecMode.Server);

                    return CreateMockTransportFactory(CreateMockTransportWhichReturnsBytes(expectedResponseMessageBytes).Object).Object;
                }).As<IClientTransportFactory>().SingleInstance();
            });

            var builder = Resolve<IMTProtoClientBuilder>();
            using (IMTProtoClientConnection connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.SetAuthInfo(authInfo);
                connection.SetSessionId(sessionId);

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
            ulong sessionId = Fixture.Create<ulong>();
            var authInfo = new AuthInfo(AuthKey, Fixture.Create<ulong>());
            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};

            Override(b =>
            {
                b.Register(context =>
                {
                    var messageCodec = context.Resolve<IMessageCodec>();
                    var authKeysProvider = Resolve<IAuthKeysProvider>();

                    AuthKeyWithId authKeyWithId = authKeysProvider.Add(authInfo.AuthKey);

                    var messageEnvelope = MessageEnvelope.CreateEncrypted(new MTProtoSessionTag(authKeyWithId.AuthKeyId, sessionId),
                        authInfo.Salt,
                        new Message(0x0102030405060708, Fixture.Create<uint>(), expectedResponse));

                    byte[] expectedResponseMessageBytes = messageCodec.EncodeEncryptedMessage(messageEnvelope,
                        authInfo.AuthKey,
                        MessageCodecMode.Server);

                    return CreateMockTransportFactory(CreateMockTransportWhichReturnsBytes(expectedResponseMessageBytes).Object).Object;
                }).As<IClientTransportFactory>().SingleInstance();
            });

            var builder = Resolve<IMTProtoClientBuilder>();
            using (IMTProtoClientConnection connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.SetAuthInfo(authInfo);
                connection.SetSessionId(sessionId);

                connection.Transport.SendingTimeout = TimeSpan.FromSeconds(5);

                await connection.ConnectAsync();

                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.EncryptedAndContentRelated);
                response.Should().Be(expectedResponse);

                await connection.DisconnectAsync();
            }
        }

        [Test]
        public async Task Should_send_plain_message_and_wait_for_response()
        {
            var testId = Fixture.Create<int>();

            var request = new TestRequest { TestId = testId };
            var expectedResponse = new TestResponse { TestId = testId, TestText = "Number 1" };

            Override(b =>
            {
                b.Register(context =>
                {
                    var messageProcessor = context.Resolve<IMessageCodec>();

                    byte[] expectedResponseMessageBytes = messageProcessor.EncodePlainMessage(new Message(0x0102030405060708, 0, expectedResponse));

                    return CreateMockTransportFactory(CreateMockTransportWhichReturnsBytes(expectedResponseMessageBytes).Object).Object;
                }).As<IClientTransportFactory>().SingleInstance();
            });

            var builder = Resolve<IMTProtoClientBuilder>();
            using (IMTProtoClientConnection connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
            {
                connection.Transport.SendingTimeout = TimeSpan.FromSeconds(5);
                await connection.ConnectAsync();

                // Testing sending a plain message.
                TestResponse response = await connection.RequestAsync<TestResponse>(request, MessageSendingFlags.None);
                response.Should().Be(expectedResponse);

                await connection.DisconnectAsync();
            }
        }

        [Test]
        public void Should_throw_on_response_timeout()
        {
            Override(builder => builder.RegisterInstance(CreateMockTransportFactory(CreateMockTransport().Object).Object));

            var testAction = new Func<Task>(async () =>
            {
                var builder = Resolve<IMTProtoClientBuilder>();
                using (IMTProtoClientConnection connection = builder.BuildConnection(Mock.Of<IClientTransportConfig>()))
                {
                    await connection.ConnectAsync();
                    await connection.RequestAsync<TestResponse>(new TestRequest(), MessageSendingFlags.None, TimeSpan.FromSeconds(1));
                }
            });
            testAction.ShouldThrow<TimeoutException>();
        }

        private Mock<IConnectableClientTransport> CreateMockTransportWhichReturnsBytes(byte[] expectedResponseMessageBytes)
        {
            var inConnector = new Subject<IBytesBucket>();
            Mock<IConnectableClientTransport> mockTransport = CreateMockTransport();

            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<IBytesBucket>>()))
                .Callback<IObserver<IBytesBucket>>(observer => inConnector.Subscribe(observer));

            mockTransport.Setup(transport => transport.SendAsync(It.IsAny<IBytesBucket>(), It.IsAny<CancellationToken>()))
                .Callback(
                    () =>
                        inConnector.OnNext(Mock.Of<IBytesBucket>(bucket => bucket.UsedBytes == new ArraySegment<byte>(expectedResponseMessageBytes))))
                .Returns(() => TaskConstants.Completed);

            return mockTransport;
        }

        private static Mock<IConnectableClientTransport> CreateMockTransport()
        {
            var mockTransport = new Mock<IConnectableClientTransport>();

            mockTransport.Setup(transport => transport.ConnectAsync()).Returns(() => Task.FromResult(TransportConnectResult.Success));
            mockTransport.Setup(transport => transport.IsConnected).Returns(() => true);

            return mockTransport;
        }

        private static Mock<IClientTransportFactory> CreateMockTransportFactory(IConnectableClientTransport clientTransport)
        {
            var mockTransportFactory = new Mock<IClientTransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<IClientTransportConfig>())).Returns(() => clientTransport);
            return mockTransportFactory;
        }
    }
}
