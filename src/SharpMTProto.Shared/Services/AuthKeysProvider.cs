// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.Collections.Concurrent;
    using Messaging;

    public interface IAuthKeysProvider
    {
        bool TryAdd(byte[] authKey, out UInt64 authKeyId);
        bool TryGet(ulong authKeyId, out byte[] authKey);
    }

    public class AuthKeysProvider : IAuthKeysProvider
    {
        private readonly ConcurrentDictionary<UInt64, byte[]> _authKeys = new ConcurrentDictionary<ulong, byte[]>();
        private readonly IMessageCodec _messageCodec;

        public AuthKeysProvider(IMessageCodec messageCodec)
        {
            _messageCodec = messageCodec;
        }

        public bool TryAdd(byte[] authKey, out UInt64 authKeyId)
        {
            authKeyId = _messageCodec.ComputeAuthKeyId(authKey);
            return _authKeys.TryAdd(authKeyId, authKey);
        }

        public bool TryGet(ulong authKeyId, out byte[] authKey)
        {
            return _authKeys.TryGetValue(authKeyId, out authKey);
        }
    }
}