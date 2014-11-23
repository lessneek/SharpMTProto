// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthKeyNegotiatorFacts.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using Nito.AsyncEx;
using NUnit.Framework;
using SharpMTProto.Authentication;
using SharpMTProto.Messaging;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

namespace SharpMTProto.Tests.Authentication
{
    [TestFixture]
    [Category("Authentication")]
    public class AuthKeyNegotiatorFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);
        }

        [Test]
        public async Task Should_create_auth_key()
        {
            IServiceLocator serviceLocator = ServiceLocator.Default;

            serviceLocator.RegisterInstance(Mock.Of<IClientTransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterInstance<INonceGenerator>(new TestNonceGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();
            serviceLocator.RegisterType<IRandomGenerator, RandomGenerator>();
            serviceLocator.RegisterType<IMessageCodec, MessageCodec>();
            serviceLocator.RegisterType<IMTProtoClientConnection, MTProtoClientConnection>(RegistrationType.Transient);
            serviceLocator.RegisterType<IMTProtoClientBuilder, MTProtoClientBuilder>();
            serviceLocator.RegisterType<IKeyChain, KeyChain>();

            // Mock transport.
            {
                var inTransport = new Subject<byte[]>();
                var mockTransport = new Mock<IClientTransport>();
                mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>()))
                    .Callback<IObserver<byte[]>>(observer => inTransport.Subscribe(observer));

                mockTransport.Setup(transport => transport.SendAsync(TestData.ReqPQ, It.IsAny<CancellationToken>()))
                    .Callback(() => inTransport.OnNext(TestData.ResPQ))
                    .Returns(() => TaskConstants.Completed);

                mockTransport.Setup(
                    transport => transport.SendAsync(TestData.ReqDHParams, It.IsAny<CancellationToken>()))
                    .Callback(() => inTransport.OnNext(TestData.ServerDHParams))
                    .Returns(() => TaskConstants.Completed);

                mockTransport.Setup(
                    transport => transport.SendAsync(TestData.SetClientDHParams, It.IsAny<CancellationToken>()))
                    .Callback(() => inTransport.OnNext(TestData.DhGenOk))
                    .Returns(() => TaskConstants.Completed);

                var mockTransportFactory = new Mock<IClientTransportFactory>();
                mockTransportFactory.Setup(factory => factory.CreateTransport(It.IsAny<IClientTransportConfig>()))
                    .Returns(mockTransport.Object);

                serviceLocator.RegisterInstance(mockTransportFactory.Object);
            }

            // Mock encryption services.
            {
                var mockEncryptionServices = new Mock<IEncryptionServices>();
                mockEncryptionServices.Setup(services => services.RSAEncrypt(It.IsAny<byte[]>(), It.IsAny<PublicKey>()))
                    .Returns(TestData.EncryptedData);
                mockEncryptionServices.Setup(
                    services =>
                        services.Aes256IgeDecrypt(TestData.ServerDHParamsOkEncryptedAnswer,
                            TestData.TmpAesKey,
                            TestData.TmpAesIV))
                    .Returns(TestData.ServerDHInnerDataWithHash);
                mockEncryptionServices.Setup(
                    services =>
                        services.Aes256IgeEncrypt(
                            It.Is<byte[]>(
                                bytes =>
                                    bytes.RewriteWithValue(0, bytes.Length - 12, 12)
                                        .SequenceEqual(TestData.ClientDHInnerDataWithHash)),
                            TestData.TmpAesKey,
                            TestData.TmpAesIV)).Returns(TestData.SetClientDHParamsEncryptedData);
                mockEncryptionServices.Setup(services => services.DH(TestData.B, TestData.G, TestData.GA, TestData.P))
                    .Returns(new DHOutParams(TestData.GB, TestData.AuthKey));

                serviceLocator.RegisterInstance(mockEncryptionServices.Object);
            }

            var keyChain = serviceLocator.ResolveType<IKeyChain>();
            keyChain.AddKeys(TestData.TestPublicKeys);

            var mtProtoBuilder = serviceLocator.ResolveType<IMTProtoClientBuilder>();
            IAuthKeyNegotiator authKeyNegotiator = mtProtoBuilder.BuildAuthKeyNegotiator(Mock.Of<IClientTransportConfig>());

            AuthInfo authInfo = await authKeyNegotiator.CreateAuthKey();

            authInfo.AuthKey.Should().Equal(TestData.AuthKey);
            authInfo.Salt.Should().Be(TestData.InitialSalt);
        }
    }
}
