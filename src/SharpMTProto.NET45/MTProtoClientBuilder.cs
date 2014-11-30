// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using SharpMTProto.Annotations;
using SharpMTProto.Authentication;
using SharpMTProto.Messaging;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

namespace SharpMTProto
{
    using Messaging.Handlers;

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
            var messageDispatcher = new MessageDispatcher();

            return new MTProtoClientBuilder(clientTransportFactory,
                tlRig,
                messageIdGenerator,
                messageCodec,
                hashServices,
                encryptionServices,
                nonceGenerator,
                keyChain,
                messageDispatcher);
        }
    }
}
