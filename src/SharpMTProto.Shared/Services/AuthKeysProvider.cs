// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.Collections.Immutable;
    using Messaging;

    public interface IAuthKeysProvider
    {
        void Add(byte[] authKeyBytes, out AuthKeyWithId authKeyId);
        bool TryGet(ulong authKeyId, out AuthKeyWithId authKeyWithId);
    }

    public class AuthKeysProvider : IAuthKeysProvider
    {
        private ImmutableDictionary<UInt64, AuthKeyWithId> _authKeys = ImmutableDictionary<ulong, AuthKeyWithId>.Empty;
        private readonly IMessageCodec _messageCodec;

        public AuthKeysProvider(IMessageCodec messageCodec)
        {
            _messageCodec = messageCodec;
        }

        public void Add(byte[] authKeyBytes, out AuthKeyWithId authKeyWithId)
        {
            ulong authKeyId = _messageCodec.ComputeAuthKeyId(authKeyBytes);
            authKeyWithId = ImmutableInterlocked.GetOrAdd(ref _authKeys, authKeyId, arg => new AuthKeyWithId(arg, authKeyBytes));
        }

        public bool TryGet(ulong authKeyId, out AuthKeyWithId authKeyWithId)
        {
            return _authKeys.TryGetValue(authKeyId, out authKeyWithId);
        }
    }
}
