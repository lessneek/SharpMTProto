// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthKeyNegotiatorFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests.Authentication
{
    using System;
    using System.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using BigMath.Utils;
    using FluentAssertions;
    using Moq;
    using Nito.AsyncEx;
    using NUnit.Framework;
    using SetUp;
    using SharpMTProto.Authentication;
    using SharpMTProto.Services;
    using SharpMTProto.Transport;

    [TestFixture]
    [Category("Authentication")]
    public class AuthKeyNegotiatorFacts : SharpMTProtoTestBase
    {
        [Test]
        public async Task Should_create_auth_key()
        {
            Override(builder =>
            {
                builder.RegisterType<TestNonceGenerator>().As<INonceGenerator>().SingleInstance();
                builder.RegisterInstance(CreateMockClientTransportFactory().Object).As<IClientTransportFactory>().SingleInstance();
                builder.RegisterInstance(CreateMockEncryptionServices().Object).As<IEncryptionServices>().SingleInstance();
            });

            var keyChain = Resolve<IKeyChain>();
            keyChain.AddKeys(TestData.TestPublicKeys);

            var mtProtoBuilder = Resolve<IMTProtoClientBuilder>();
            IAuthKeyNegotiator authKeyNegotiator = mtProtoBuilder.BuildAuthKeyNegotiator(Mock.Of<IClientTransportConfig>());

            AuthInfo authInfo = await authKeyNegotiator.CreateAuthKey();

            authInfo.AuthKey.Should().Equal(TestData.AuthKey);
            authInfo.Salt.Should().Be(TestData.InitialSalt);
        }

        private static Mock<IEncryptionServices> CreateMockEncryptionServices()
        {
            var mockEncryptionServices = new Mock<IEncryptionServices>();
            mockEncryptionServices.Setup(services => services.RSAEncrypt(It.IsAny<byte[]>(), It.IsAny<PublicKey>())).Returns(TestData.EncryptedData);

            mockEncryptionServices.Setup(
                services => services.Aes256IgeDecrypt(TestData.ServerDHParamsOkEncryptedAnswer, TestData.TmpAesKey, TestData.TmpAesIV))
                .Returns(TestData.ServerDHInnerDataWithHash);

            mockEncryptionServices.Setup(
                services =>
                    services.Aes256IgeEncrypt(
                        It.Is<byte[]>(bytes => bytes.RewriteWithValue(0, bytes.Length - 12, 12).SequenceEqual(TestData.ClientDHInnerDataWithHash)),
                        TestData.TmpAesKey, TestData.TmpAesIV)).Returns(TestData.SetClientDHParamsEncryptedData);

            mockEncryptionServices.Setup(services => services.DH(TestData.B, TestData.G, TestData.GA, TestData.P))
                .Returns(new DHOutParams(TestData.GB, TestData.AuthKey));

            return mockEncryptionServices;
        }

        private static Mock<IClientTransportFactory> CreateMockClientTransportFactory()
        {
            var inTransport = new Subject<byte[]>();
            var mockTransport = new Mock<IClientTransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>()))
                .Callback<IObserver<byte[]>>(observer => inTransport.Subscribe(observer));

            mockTransport.Setup(transport => transport.ConnectAsync()).Returns(() => Task.FromResult(TransportConnectResult.Success));

            mockTransport.Setup(transport => transport.IsConnected).Returns(() => true);

            mockTransport.Setup(transport => transport.SendAsync(TestData.ReqPQ, It.IsAny<CancellationToken>()))
                .Callback(() => inTransport.OnNext(TestData.ResPQ))
                .Returns(() => TaskConstants.Completed);

            mockTransport.Setup(transport => transport.SendAsync(TestData.ReqDHParams, It.IsAny<CancellationToken>()))
                .Callback(() => inTransport.OnNext(TestData.ServerDHParams))
                .Returns(() => TaskConstants.Completed);

            mockTransport.Setup(transport => transport.SendAsync(TestData.SetClientDHParams, It.IsAny<CancellationToken>()))
                .Callback(() => inTransport.OnNext(TestData.DhGenOk))
                .Returns(() => TaskConstants.Completed);

            var mockTransportFactory = new Mock<IClientTransportFactory>();
            mockTransportFactory.Setup(factory => factory.CreateTransport(It.IsAny<IClientTransportConfig>())).Returns(mockTransport.Object);

            return mockTransportFactory;
        }
    }
}
