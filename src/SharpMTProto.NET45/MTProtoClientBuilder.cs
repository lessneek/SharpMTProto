// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using Annotations;
    using Authentication;
    using Dataflows;
    using Messaging;
    using Services;
    using SharpTL;
    using Transport;
    using Transport.Packets;

    public partial class MTProtoClientBuilder
    {
        [NotNull]
        private static MTProtoClientBuilder CreateDefault()
        {
            // TODO: bytes ocean.
            IBytesOcean bytesOcean = BytesOcean.WithBuckets(100, Defaults.MaximumMessageLength).Build();

            var clientTransportFactory =
                new ClientTransportFactory(config => new TcpClientTransport(config, new TcpFullTransportPacketProcessor(bytesOcean), bytesOcean));
            var tlRig = new TLRig();
            var messageIdGenerator = new MessageIdGenerator();
            var hashServices = new HashServices();
            var encryptionServices = new EncryptionServices();
            var randomGenerator = new RandomGenerator();
            var messageCodec = new MessageCodec(tlRig, hashServices, encryptionServices, randomGenerator, bytesOcean);
            var keyChain = new KeyChain(tlRig, hashServices);
            var nonceGenerator = new NonceGenerator();
            var authKeysProvider = new AuthKeysProvider(messageCodec);

            return new MTProtoClientBuilder(clientTransportFactory,
                tlRig,
                messageIdGenerator,
                messageCodec,
                hashServices,
                encryptionServices,
                nonceGenerator,
                keyChain,
                authKeysProvider);
        }
    }
}
