// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientBuilder.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#region R#

// ReSharper disable MemberCanBePrivate.Global

#endregion

namespace SharpMTProto
{
    using Annotations;
    using Authentication;
    using Messaging;
    using Services;
    using SharpTL;
    using Transport;

    public interface IMTProtoClientBuilder
    {
        [NotNull]
        IMTProtoClientConnection BuildConnection([NotNull] IClientTransportConfig clientTransportConfig);

        [NotNull]
        IAuthKeyNegotiator BuildAuthKeyNegotiator([NotNull] IClientTransportConfig clientTransportConfig);
    }

    public partial class MTProtoClientBuilder : IMTProtoClientBuilder
    {
        public static readonly IMTProtoClientBuilder Default;

        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IKeyChain _keyChain;
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly INonceGenerator _nonceGenerator;
        private readonly TLRig _tlRig;
        private readonly IClientTransportFactory _clientTransportFactory;

        static MTProtoClientBuilder()
        {
            Default = CreateDefault();
        }

        public MTProtoClientBuilder([NotNull] IClientTransportFactory clientTransportFactory,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec,
            [NotNull] IHashServices hashServices,
            [NotNull] IEncryptionServices encryptionServices,
            [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IKeyChain keyChain,
            [NotNull] IAuthKeysProvider authKeysProvider)
        {
            _clientTransportFactory = clientTransportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _nonceGenerator = nonceGenerator;
            _keyChain = keyChain;
            _authKeysProvider = authKeysProvider;
        }

        IMTProtoClientConnection IMTProtoClientBuilder.BuildConnection(IClientTransportConfig clientTransportConfig)
        {
            IClientTransport transport = _clientTransportFactory.CreateTransport(clientTransportConfig);
            var messenger = new MTProtoMessenger(transport, _messageIdGenerator, _messageCodec, _authKeysProvider);
            return new MTProtoClientConnection(messenger);
        }

        IAuthKeyNegotiator IMTProtoClientBuilder.BuildAuthKeyNegotiator(IClientTransportConfig clientTransportConfig)
        {
            return new AuthKeyNegotiator(clientTransportConfig, this, _tlRig, _nonceGenerator, _hashServices, _encryptionServices, _keyChain);
        }

        [NotNull]
        public static IMTProtoClientConnection BuildConnection([NotNull] IClientTransportConfig clientTransportConfig)
        {
            return Default.BuildConnection(clientTransportConfig);
        }

        [NotNull]
        public static IAuthKeyNegotiator BuildAuthKeyNegotiator([NotNull] IClientTransportConfig clientTransportConfig)
        {
            return Default.BuildAuthKeyNegotiator(clientTransportConfig);
        }
    }
}
