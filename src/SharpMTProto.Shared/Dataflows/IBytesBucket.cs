//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;

    /// <summary>
    ///     A bucket with bytes.
    /// </summary>
    public interface IBytesBucket : IDisposable
    {
        /// <summary>
        ///     Total bytes bucket size.
        /// </summary>
        int Size { get; }

        /// <summary>
        ///     Base bytes array segment.
        /// </summary>
        ArraySegment<byte> Bytes { get; }

        /// <summary>
        ///     Origin of bytes bucket. Not null in case bucket is taken from ocean, otherwise can be null.
        /// </summary>
        IBytesOcean Ocean { get; }

        /// <summary>
        ///     Represents used bytes count within <see cref="Bytes" /> array segment.
        /// </summary>
        int Used { get; set; }

        /// <summary>
        ///     Used bytes array segment. Represents projection of <see cref="Used" /> and <see cref="Offset" /> properties.
        /// </summary>
        ArraySegment<byte> UsedBytes { get; }

        /// <summary>
        ///     Offset in base <see cref="Bytes" /> array segment.
        /// </summary>
        int Offset { get; set; }

        /// <summary>
        ///     True - when bytes bucket is taken from bytes ocean.
        /// </summary>
        bool IsTaken { get; }

        /// <summary>
        ///     Cleans up bytes array segment <see cref="Bytes" /> and sets <see cref="Used" /> and <see cref="Offset" /> to zero.
        ///     Does not trigger return to a <see cref="Ocean" />.
        /// </summary>
        void CleanUp();
    }
}
