///////////////////////////////////////////////////////////////
// Copyright (c) 2014 Alexander Logger. All rights reserved. //
///////////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class BytesOcean : IBytesOcean
    {
        private readonly Dictionary<int, BufferBlock<BytesBucket>> _buckets;
        private readonly byte[] _bytes;
        private readonly BytesOceanConfig _config;
        private readonly int _maximalBucketSize;
        private readonly int _minimalBucketSize = int.MaxValue;

        public BytesOcean(BytesOceanConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            _config = config;

            _bytes = new byte[_config.TotalSize];
            _buckets = new Dictionary<int, BufferBlock<BytesBucket>>(_config.BucketSizes);

            var tip = 0;
            foreach (BytesBucketsConfig bucketsConfig in _config.BucketsConfigs.OrderBy(c => c.BucketSize))
            {
                int bucketsCount = bucketsConfig.Count;
                int bucketSize = bucketsConfig.BucketSize;

                _minimalBucketSize = bucketSize < _minimalBucketSize ? bucketSize : _minimalBucketSize;
                _maximalBucketSize = bucketSize > _maximalBucketSize ? bucketSize : _maximalBucketSize;

                BufferBlock<BytesBucket> bytesBuckets;
                if (!_buckets.TryGetValue(bucketSize, out bytesBuckets))
                {
                    bytesBuckets = new BufferBlock<BytesBucket>();
                    _buckets[bucketSize] = bytesBuckets;
                }

                for (var bucketIndex = 0; bucketIndex < bucketsCount; bucketIndex++)
                {
                    var arraySegment = new ArraySegment<byte>(_bytes, tip, bucketSize);
                    bytesBuckets.Post(new BytesBucket(this, arraySegment));
                    tip += bucketSize;
                }
            }
        }

        public int Size
        {
            get { return _config.TotalSize; }
        }

        public int MinimalBucketSize
        {
            get { return _minimalBucketSize; }
        }

        public int MaximalBucketSize
        {
            get { return _maximalBucketSize; }
        }

        public async Task<IBytesBucket> TakeAsync(int minimalSize, CancellationToken cancellationToken)
        {
            if (minimalSize > _maximalBucketSize)
                throw new ArgumentOutOfRangeException("minimalSize", minimalSize, "Couldn't take bucket with such minimal size.");

            cancellationToken.ThrowIfCancellationRequested();

            int sizeKey = _buckets.Keys.FirstOrDefault(size => size >= minimalSize);
            BytesBucket bucket = await _buckets[sizeKey].ReceiveAsync(cancellationToken).ConfigureAwait(false);
            bucket.SetTaken();
            return bucket;
        }

        public static IBytesOceanConfigurator WithBuckets(int count, int bucketSize)
        {
            return new BytesOceanConfigurator().WithBuckets(count, bucketSize);
        }

        private void Return(BytesBucket bytesBucket)
        {
            _buckets[bytesBucket.Size].Post(bytesBucket);
        }

        private class BytesBucket : BytesBucketBase
        {
            private const int Taken = 1;
            private const int NotTaken = 0;

            private static readonly InvalidOperationException InvalidOffsetAndUsedException =
                new InvalidOperationException("Offset + used bytes must not exceed whole bucket size.");

            private static readonly InvalidOperationException InvalidUsedException =
                new InvalidOperationException("Used bytes count must be zero or higher.");

            private int _isTaken;
            private readonly BytesOcean _ocean;

            public BytesBucket(BytesOcean ocean, ArraySegment<byte> bytes) : base(bytes)
            {
                _ocean = ocean;
            }

            public override bool IsTaken
            {
                get { return _isTaken == Taken; }
            }

            public override void Dispose()
            {
                if (Interlocked.CompareExchange(ref _isTaken, NotTaken, Taken) == NotTaken)
                    return;

                base.Dispose();
                _ocean.Return(this);
            }

            public override IBytesOcean Ocean
            {
                get { return _ocean; }
            }

            public void SetTaken()
            {
                Interlocked.CompareExchange(ref _isTaken, Taken, NotTaken);
            }

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
}
