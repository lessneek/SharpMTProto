// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using Annotations;
    using Authentication;
    using Messaging;
    using Services;
    using SharpTL;
    using Transport;

    public partial class MTProtoClientBuilder
    {
        [NotNull]
        private static MTProtoClientBuilder CreateDefault()
        {
            var clientTransportFactory =
                new ClientTransportFactory(config => new TcpClientTransport(config, new TcpTransportFullPacketProcessor()));
            var tlRig = new TLRig();
            var messageIdGenerator = new MessageIdGenerator();
            var hashServiceProvider = new SystemHashServiceProvider();
            var encryptionServices = new EncryptionServices();
            var randomGenerator = new RandomGenerator();
            var authKeysProvider = new AuthKeysProvider(hashServiceProvider);
            var messageCodec = new MessageCodec(tlRig, hashServiceProvider, encryptionServices, randomGenerator, authKeysProvider);
            var keyChain = new KeyChain(tlRig, hashServiceProvider);
            var nonceGenerator = new NonceGenerator();

            return new MTProtoClientBuilder(clientTransportFactory,
                tlRig,
                messageIdGenerator,
                messageCodec,
                hashServiceProvider,
                encryptionServices,
                nonceGenerator,
                keyChain,
                authKeysProvider,
                randomGenerator);
        }
    }
}
