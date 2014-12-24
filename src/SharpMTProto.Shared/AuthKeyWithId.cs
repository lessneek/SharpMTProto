//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto
{
    public class AuthKeyWithId
    {
        public AuthKeyWithId(ulong id, byte[] value)
        {
            Id = id;
            Value = value;
        }

        public ulong Id { get; private set; }
        public byte[] Value { get; private set; }
    }
}
