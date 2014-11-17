// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

// ReSharper disable MemberCanBePrivate.Global

using SharpMTProto.Annotations;
using SharpMTProto.Messaging;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

namespace SharpMTProto
{
    public interface IMTProtoBuilder
    {
        [NotNull]
        IMTProtoConnection BuildConnection([NotNull] TransportConfig transportConfig);
    }

    public partial class MTProtoBuilder : IMTProtoBuilder
    {
        public static readonly IMTProtoBuilder Default;

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

        public IMTProtoConnection BuildConnection(TransportConfig transportConfig)
        {
            return new MTProtoConnection(transportConfig, _transportFactory, _tlRig, _messageIdGenerator, _messageCodec);
        }
    }
}
