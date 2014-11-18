// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using SharpMTProto.Annotations;
using SharpMTProto.Authentication;
using SharpMTProto.Messaging;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

// ReSharper disable MemberCanBePrivate.Global

namespace SharpMTProto
{
    public interface IMTProtoBuilder
    {
        [NotNull]
        IMTProtoConnection BuildConnection([NotNull] ITransportConfig transportConfig);

        [NotNull]
        IAuthKeyNegotiator BuildAuthKeyNegotiator([NotNull] ITransportConfig transportConfig);
    }

    public partial class MTProtoBuilder : IMTProtoBuilder
    {
        public static readonly IMTProtoBuilder Default;

        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IKeyChain _keyChain;
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly INonceGenerator _nonceGenerator;
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
            [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IKeyChain keyChain)
        {
            _transportFactory = transportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _nonceGenerator = nonceGenerator;
            _keyChain = keyChain;
        }

        IMTProtoConnection IMTProtoBuilder.BuildConnection(ITransportConfig transportConfig)
        {
            return new MTProtoConnection(transportConfig, _transportFactory, _tlRig, _messageIdGenerator, _messageCodec);
        }

        IAuthKeyNegotiator IMTProtoBuilder.BuildAuthKeyNegotiator(ITransportConfig transportConfig)
        {
            return new AuthKeyNegotiator(transportConfig,
                this,
                _tlRig,
                _nonceGenerator,
                _hashServices,
                _encryptionServices,
                _keyChain);
        }

        [NotNull]
        public static IMTProtoConnection BuildConnection([NotNull] ITransportConfig transportConfig)
        {
            return Default.BuildConnection(transportConfig);
        }

        [NotNull]
        public static IAuthKeyNegotiator BuildAuthKeyNegotiator([NotNull] ITransportConfig transportConfig)
        {
            return Default.BuildAuthKeyNegotiator(transportConfig);
        }
    }
}
