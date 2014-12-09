// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using Messaging;

    public interface IAuthKeysProvider
    {
        void Add(byte[] authKey, out UInt64 authKeyId);
        bool TryGet(ulong authKeyId, out byte[] authKey);
    }

    public class AuthKeysProvider : IAuthKeysProvider
    {
        private volatile ImmutableDictionary<UInt64, byte[]> _authKeys = ImmutableDictionary<ulong, byte[]>.Empty;
        private readonly IMessageCodec _messageCodec;
        private readonly object _syncRoot = new object();

        public AuthKeysProvider(IMessageCodec messageCodec)
        {
            _messageCodec = messageCodec;
        }

        public void Add(byte[] authKey, out UInt64 authKeyId)
        {
            authKeyId = _messageCodec.ComputeAuthKeyId(authKey);

            lock (_syncRoot)
            {
                if (_authKeys.ContainsKey(authKeyId))
                    return;

                _authKeys = _authKeys.Add(authKeyId, authKey);
            }
        }

        public bool TryGet(ulong authKeyId, out byte[] authKey)
        {
            return _authKeys.TryGetValue(authKeyId, out authKey);
        }
    }
}