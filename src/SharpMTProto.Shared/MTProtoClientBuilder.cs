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
    using System;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
    using SharpMTProto.Services;
    using SharpMTProto.Transport;
    using SharpTL;

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
        private readonly IAuthKeysProvider _authKeysProvider;
        private readonly IClientTransportFactory _clientTransportFactory;
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServiceProvider _hashServiceProvider;
        private readonly IKeyChain _keyChain;
        private readonly IMessageCodec _messageCodec;
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly INonceGenerator _nonceGenerator;
        private readonly IRandomGenerator _randomGenerator;
        private readonly TLRig _tlRig;

        static MTProtoClientBuilder()
        {
            Default = CreateDefault();
        }

        public MTProtoClientBuilder([NotNull] IClientTransportFactory clientTransportFactory,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec,
            [NotNull] IHashServiceProvider hashServiceProvider,
            [NotNull] IEncryptionServices encryptionServices,
            [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IKeyChain keyChain,
            [NotNull] IAuthKeysProvider authKeysProvider,
            [NotNull] IRandomGenerator randomGenerator)
        {
            if (clientTransportFactory == null)
                throw new ArgumentNullException("clientTransportFactory");
            if (tlRig == null)
                throw new ArgumentNullException("tlRig");
            if (messageIdGenerator == null)
                throw new ArgumentNullException("messageIdGenerator");
            if (messageCodec == null)
                throw new ArgumentNullException("messageCodec");
            if (hashServiceProvider == null)
                throw new ArgumentNullException("hashServiceProvider");
            if (encryptionServices == null)
                throw new ArgumentNullException("encryptionServices");
            if (nonceGenerator == null)
                throw new ArgumentNullException("nonceGenerator");
            if (keyChain == null)
                throw new ArgumentNullException("keyChain");
            if (authKeysProvider == null)
                throw new ArgumentNullException("authKeysProvider");
            if (randomGenerator == null)
                throw new ArgumentNullException("randomGenerator");

            _clientTransportFactory = clientTransportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _messageCodec = messageCodec;
            _hashServiceProvider = hashServiceProvider;
            _encryptionServices = encryptionServices;
            _nonceGenerator = nonceGenerator;
            _keyChain = keyChain;
            _authKeysProvider = authKeysProvider;
            _randomGenerator = randomGenerator;
        }

        IMTProtoClientConnection IMTProtoClientBuilder.BuildConnection(IClientTransportConfig clientTransportConfig)
        {
            IClientTransport transport = _clientTransportFactory.CreateTransport(clientTransportConfig);

            // TODO: add bytes ocean external config.
            return new MTProtoClientConnection(transport,
                _messageIdGenerator,
                new MTProtoSession(_messageIdGenerator, _randomGenerator, _authKeysProvider),
                new MTProtoMessenger(_messageCodec));
        }

        IAuthKeyNegotiator IMTProtoClientBuilder.BuildAuthKeyNegotiator(IClientTransportConfig clientTransportConfig)
        {
            return new AuthKeyNegotiator(clientTransportConfig, this, _tlRig, _nonceGenerator, _hashServiceProvider, _encryptionServices, _keyChain);
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
