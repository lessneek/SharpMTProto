//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class BytesOceanConfig
    {
        public BytesOceanConfig(params BytesBucketsConfig[] bucketsConfigs)
        {
            if (bucketsConfigs == null)
                throw new ArgumentNullException("bucketsConfigs");
            if (bucketsConfigs.Length <= 0)
                throw new ArgumentOutOfRangeException("bucketsConfigs", "At least one BytesBucketsConfig must be set.");

            BucketsConfigs = bucketsConfigs;
            BucketSizes = bucketsConfigs.Length;

            long totalSize = (from bc in BucketsConfigs select bc.TotalSize).Aggregate(0L, (arg1, arg2) => arg1 + arg2);
            if (totalSize > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(string.Format("Total size of bytes ocean must not exñeed {0}. Actual total size is {1}.",
                    int.MaxValue,
                    totalSize));
            }
            TotalSize = (int) totalSize;
        }

        public IEnumerable<BytesBucketsConfig> BucketsConfigs { get; private set; }
        public int TotalSize { get; private set; }
        public int BucketSizes { get; private set; }
    }

    public struct BytesBucketsConfig
    {
        public BytesBucketsConfig(int count, int bucketSize) : this()
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException("count", count, "Count must be greater than zero.");
            if (bucketSize <= 0)
                throw new ArgumentOutOfRangeException("bucketSize", bucketSize, "Bucket size must be greater than zero.");

            Count = count;
            BucketSize = bucketSize;
            long totalSize = Count*(long) BucketSize;
            if (totalSize > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(string.Format("Total size of buckets must not exñeed {0}. Actual total size is {1}.",
                    int.MaxValue,
                    totalSize));
            }

            TotalSize = (int) totalSize;
        }

        public int Count { get; private set; }
        public int BucketSize { get; private set; }
        public int TotalSize { get; private set; }
    }
}
