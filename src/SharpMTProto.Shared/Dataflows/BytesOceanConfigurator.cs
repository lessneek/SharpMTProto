//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System.Collections.Generic;

    public interface IBytesOceanConfigurator
    {
        IBytesOceanConfigurator WithBuckets(int count, int bucketSize);
        IBytesOcean Build();
    }

    internal class BytesOceanConfigurator : IBytesOceanConfigurator
    {
        private readonly List<BytesBucketsConfig> _bucketsConfigs = new List<BytesBucketsConfig>();

        public IBytesOceanConfigurator WithBuckets(int count, int bucketSize)
        {
            _bucketsConfigs.Add(new BytesBucketsConfig(count, bucketSize));
            return this;
        }

        public IBytesOcean Build()
        {
            return new BytesOcean(new BytesOceanConfig(_bucketsConfigs.ToArray()));
        }
    }
}
