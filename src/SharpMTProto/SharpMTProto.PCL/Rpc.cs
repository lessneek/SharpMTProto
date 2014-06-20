// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Rpc.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    public class Rpc
    {
        public Rpc(int id, MessageSending request)
        {
            Id = id;
            Request = request;
        }

        public int Id { get; private set; }
        public MessageSending Request { get; set; }
    }
}
