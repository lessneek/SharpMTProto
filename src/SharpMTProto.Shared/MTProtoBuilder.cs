// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using SharpMTProto.Annotations;
using SharpMTProto.Messaging;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

// ReSharper disable once MemberCanBePrivate.Global

namespace SharpMTProto
{
    public class MTProtoBuilder
    {
        public static readonly MTProtoBuilder Default;

        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly IRandomGenerator _randomGenerator;
        private readonly TLRig _tlRig;
        private readonly ITransportFactory _transportFactory;

        static MTProtoBuilder()
        {
            Default = CreateDefault();
        }

        public MTProtoBuilder(
            [NotNull] ITransportFactory transportFactory,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec,
            [NotNull] IHashServices hashServices,
            [NotNull] IEncryptionServices encryptionServices,
            [NotNull] IRandomGenerator randomGenerator)
        {
            _transportFactory = transportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _randomGenerator = randomGenerator;
        }

#if !PCL
        [NotNull]
        private static MTProtoBuilder CreateDefault()
        {
            var transportFactory = new TransportFactory();
            var tlRig = new TLRig();
            var messageIdGenerator = new MessageIdGenerator();
            var hashServices = new HashServices();
            var encryptionServices = new EncryptionServices();
            var randomGenerator = new RandomGenerator();
            var messageCodec = new MessageCodec(tlRig, hashServices, encryptionServices, randomGenerator);

            return new MTProtoBuilder(transportFactory,
                tlRig,
                messageIdGenerator,
                messageCodec,
                hashServices,
                encryptionServices,
                randomGenerator);
        }
#else
        private static MTProtoBuilder CreateDefault()
        {
            throw new PlatformNotSupportedException();
        }
#endif

        public IMTProtoConnection BuildConnection(TransportConfig transportConfig)
        {
            return new MTProtoConnection(transportConfig, _transportFactory, _tlRig, _messageIdGenerator, _messageCodec);
        }
    }
}