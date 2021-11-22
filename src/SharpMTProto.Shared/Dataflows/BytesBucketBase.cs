//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;

    public abstract class BytesBucketBase : IBytesBucket
    {
        private static readonly InvalidOperationException InvalidOffsetAndUsedException =
            new InvalidOperationException("Offset + used bytes must not exceed whole bucket size.");

        private static readonly InvalidOperationException InvalidUsedException =
            new InvalidOperationException("Used bytes count must be zero or higher.");

        private int _offset;
        private int _used;
        private readonly ArraySegment<byte> _bytes;

        protected BytesBucketBase(ArraySegment<byte> bytes)
        {
            _bytes = bytes;
        }

        public abstract bool IsTaken { get; }

        public int Offset
        {
            get { return _offset; }
            set
            {
                if (value + _used > _bytes.Count)
                {
                    ThrowInvalidOffsetAndUsed();
                }
                _offset = value;
            }
        }

        public ArraySegment<byte> UsedBytes
        {
            get { return new ArraySegment<byte>(_bytes.Array, _bytes.Offset + _offset, _used); }
        }

        public virtual void Dispose()
        {
            CleanUp();
        }

        public void CleanUp()
        {
            Used = 0;
            Offset = 0;
            Array.Clear(_bytes.Array, _bytes.Offset, _bytes.Count);
        }

        public int Size
        {
            get { return _bytes.Count; }
        }

        public int Used
        {
            get { return _used; }
            set
            {
                if (value < 0)
                {
                    ThrowInvalidUsed();
                }
                if (value + _offset > _bytes.Count)
                {
                    ThrowInvalidOffsetAndUsed();
                }
                _used = value;
            }
        }

        public ArraySegment<byte> Bytes
        {
            get { return _bytes; }
        }

        public abstract IBytesOcean Ocean { get; }

        private static void ThrowInvalidOffsetAndUsed()
        {
            throw InvalidOffsetAndUsedException;
        }

        private static void ThrowInvalidUsed()
        {
            throw InvalidUsedException;
        }
    }
}
