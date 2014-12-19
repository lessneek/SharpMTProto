//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;

    public interface IBytesBucket : IDisposable
    {
        int Size { get; }
        ArraySegment<byte> Bytes { get; }
        IBytesOcean Ocean { get; }
        int Used { get; set; }
        ArraySegment<byte> UsedBytes { get; }
        int Offset { get; set; }
    }
}
