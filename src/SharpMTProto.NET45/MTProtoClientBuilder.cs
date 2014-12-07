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
            var clientTransportFactory = new ClientTransportFactory();
            var tlRig = new TLRig();
            var messageIdGenerator = new MessageIdGenerator();
            var hashServices = new HashServices();
            var encryptionServices = new EncryptionServices();
            var randomGenerator = new RandomGenerator();
            var messageCodec = new MessageCodec(tlRig, hashServices, encryptionServices, randomGenerator);
            var keyChain = new KeyChain(tlRig, hashServices);
            var nonceGenerator = new NonceGenerator();

            return new MTProtoClientBuilder(clientTransportFactory,
                tlRig,
                messageIdGenerator,
                messageCodec,
                hashServices,
                encryptionServices,
                nonceGenerator,
                keyChain);
        }
    }
}
