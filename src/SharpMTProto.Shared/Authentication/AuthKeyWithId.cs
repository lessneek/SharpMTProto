//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Authentication
{
    public struct AuthKeyWithId
    {
        public AuthKeyWithId(ulong authKeyId, byte[] authKey) : this()
        {
            AuthKeyId = authKeyId;
            AuthKey = authKey;
        }

        public ulong AuthKeyId { get; private set; }
        public byte[] AuthKey { get; private set; }
    }
}
