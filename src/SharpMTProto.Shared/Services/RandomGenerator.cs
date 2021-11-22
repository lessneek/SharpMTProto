// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RandomGenerator.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.Threading;
    using BigMath.Utils;
    using SharpMTProto.Utils;

    public interface IRandomGenerator
    {
        void FillWithRandom(ArraySegment<byte> bytes);
        void FillWithRandom(byte[] buffer);
        ulong NextUInt64();
    }

    public class RandomGenerator : Cancelable, IRandomGenerator
    {
        private const int BufferLength = 32;
        private ThreadLocal<byte[]> _buffer = new ThreadLocal<byte[]>(() => new byte[BufferLength]);
        private ThreadLocal<Random> _random;

        public RandomGenerator()
        {
            _random = new ThreadLocal<Random>(() => new Random());
        }

        public RandomGenerator(int seed)
        {
            _random = new ThreadLocal<Random>(() => new Random(seed));
        }

        public void FillWithRandom(ArraySegment<byte> bytes)
        {
            ThrowIfDisposed();

            if (bytes.Array == null)
                throw new ArgumentNullException("bytes", "Array is null.");
            if (bytes.Offset + bytes.Count > bytes.Array.Length)
                throw new ArgumentOutOfRangeException("bytes", "Array length must be greater or equal to (offset + count).");

            int count = bytes.Count;
            int offset = bytes.Offset;
            while (count > 0)
            {
                byte[] buffer = _buffer.Value;
                _random.Value.NextBytes(buffer);
                int bytesToWrite = count > BufferLength ? BufferLength : count;
                Buffer.BlockCopy(buffer, 0, bytes.Array, offset, bytesToWrite);
                count -= bytesToWrite;
                offset += bytesToWrite;
            }
        }

        public void FillWithRandom(byte[] buffer)
        {
            FillWithRandom(new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        public ulong NextUInt64()
        {
            ThrowIfDisposed();

            byte[] buffer = _buffer.Value;
            _random.Value.NextBytes(buffer);
            return buffer.ToUInt64();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_buffer != null)
                {
                    _buffer.Dispose();
                    _buffer = null;
                }
                if (_random != null)
                {
                    _random.Dispose();
                    _random = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
