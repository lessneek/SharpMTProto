// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.Collections.Immutable;
    using BigMath.Utils;
    using SharpMTProto.Annotations;

    public interface IAuthKeysProvider
    {
        AuthKeyWithId Add(byte[] authKeyBytes);
        bool TryGet(ulong authKeyId, out AuthKeyWithId authKeyWithId);

        /// <summary>
        ///     Computes auth key id.
        /// </summary>
        /// <param name="authKey">Auth key.</param>
        /// <returns>Auth key id.</returns>
        ulong ComputeAuthKeyId(byte[] authKey);
    }

    public class AuthKeysProvider : IAuthKeysProvider
    {
        private readonly IHashService _sha1;
        private ImmutableDictionary<UInt64, AuthKeyWithId> _authKeys = ImmutableDictionary<ulong, AuthKeyWithId>.Empty;

        public AuthKeysProvider([NotNull] IHashServiceProvider hashServiceProvider)
        {
            if (hashServiceProvider == null)
                throw new ArgumentNullException("hashServiceProvider");

            _sha1 = hashServiceProvider.Create(HashServiceTag.SHA1);
        }

        public AuthKeyWithId Add(byte[] authKeyBytes)
        {
            ulong authKeyId = ComputeAuthKeyId(authKeyBytes);
            return ImmutableInterlocked.GetOrAdd(ref _authKeys, authKeyId, arg => new AuthKeyWithId(arg, authKeyBytes));
        }

        public bool TryGet(ulong authKeyId, out AuthKeyWithId authKeyWithId)
        {
            return _authKeys.TryGetValue(authKeyId, out authKeyWithId);
        }

        public ulong ComputeAuthKeyId(byte[] authKey)
        {
            byte[] authKeySHA1 = _sha1.Hash(authKey);
            return authKeySHA1.ToUInt64(authKeySHA1.Length - 8, true);
        }
    }
}
