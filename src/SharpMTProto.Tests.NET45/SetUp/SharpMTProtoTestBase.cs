//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.SetUp
{
    using Autofac;
    using BigMath.Utils;
    using Moq;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
    using SharpMTProto.Services;
    using SharpMTProto.Transport;
    using SharpTL;

    public abstract class SharpMTProtoTestBase : TestFixtureBase
    {
        protected static readonly byte[] AuthKey =
            "752BC8FC163832CB2606F7F3DC444D39A6D725761CA2FC984958E20EB7FDCE2AA1A65EB92D224CEC47EE8339AA44DF3906D79A01148CB6AACF70D53F98767EBD7EADA5A63C4229117EFBDB50DA4399C9E1A5D8B2550F263F3D43B936EF9259289647E7AAC8737C4E007C0C9108631E2B53C8900C372AD3CCA25E314FBD99AFFD1B5BCB29C5E40BB8366F1DFD07B053F1FBBBE0AA302EEEE5CF69C5A6EA7DEECDD965E0411E3F00FE112428330EBD432F228149FD2EC9B5775050F079C69CED280FE7E13B968783E3582B9C58CEAC2149039B3EF5A4265905D661879A41AF81098FBCA6D0B91D5B595E1E27E166867C155A3496CACA9FD6CF5D16DB2ADEBB2D3E"
                .HexToBytes();

        protected IFixture Fixture { get; private set; }

        [SetUp]
        public void SetUp()
        {
            Fixture = new Fixture().Customize(new AutoConfiguredMoqCustomization());
        }

        protected override void ConfigureBuilder(ContainerBuilder builder)
        {
            builder.RegisterType<TLRig>().SingleInstance();
            builder.RegisterType<TestMessageIdsGenerator>().As<IMessageIdGenerator>().SingleInstance();
            builder.RegisterType<EncryptionServices>().As<IEncryptionServices>().SingleInstance();
            builder.RegisterType<RandomGenerator>().As<IRandomGenerator>().SingleInstance();
            builder.RegisterType<MessageCodec>().As<IMessageCodec>().SingleInstance();
            builder.RegisterType<NonceGenerator>().As<INonceGenerator>().SingleInstance();
            builder.RegisterType<KeyChain>().As<IKeyChain>().SingleInstance();
            builder.RegisterType<AuthKeysProvider>().As<IAuthKeysProvider>().SingleInstance();
            builder.RegisterType<SystemHashServiceProvider>().As<IHashServiceProvider>().SingleInstance();

            builder.RegisterInstance(
                Mock.Of<IClientTransportFactory>(
                    factory => factory.CreateTransport(It.IsAny<IClientTransportConfig>()) == Mock.Of<IConnectableClientTransport>()));

            builder.RegisterType<MTProtoClientBuilder>().As<IMTProtoClientBuilder>();
            builder.RegisterType<MTProtoSession>().As<IMTProtoSession>().AsSelf();
        }
    }
}
