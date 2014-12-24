//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto
{
    public struct Session
    {
        public Session(ulong id, ulong authKeyId) : this()
        {
            Id = id;
            AuthKeyId = authKeyId;
        }

        public ulong Id { get; set; }
        public ulong AuthKeyId { get; set; }
    }
}
