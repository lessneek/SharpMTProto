//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;

    public class StandaloneBytesBucket : BytesBucketBase
    {
        public StandaloneBytesBucket(int size) : this(new ArraySegment<byte>(new byte[size]))
        {
        }

        public StandaloneBytesBucket(byte[] bytes) : this(new ArraySegment<byte>(bytes))
        {
        }

        public StandaloneBytesBucket(ArraySegment<byte> bytes) : base(bytes)
        {
            Used = bytes.Count;
        }

        public override IBytesOcean Ocean
        {
            get { return null; }
        }

        public override bool IsTaken
        {
            get { return true; }
        }
    }
}
