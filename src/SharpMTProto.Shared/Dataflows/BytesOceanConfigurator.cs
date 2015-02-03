//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;
    using System.Collections.Generic;

    public interface IBytesOceanConfigurator
    {
        IBytesOceanConfigurator WithBuckets(int count, int bucketSize);
        IBytesOcean Build();
    }

    internal class BytesOceanConfigurator : IBytesOceanConfigurator
    {
        private readonly List<BytesBucketsConfig> _bucketsConfigs = new List<BytesBucketsConfig>();
        private TimeSpan? _defaultTimeout;

        public IBytesOceanConfigurator WithBuckets(int count, int bucketSize)
        {
            _bucketsConfigs.Add(new BytesBucketsConfig(count, bucketSize));
            return this;
        }

        public IBytesOceanConfigurator WithDefaultTimeout(TimeSpan timeout)
        {
            _defaultTimeout = timeout;
            return this;
        }

        public IBytesOcean Build()
        {
            var config = new BytesOceanConfig(_bucketsConfigs.ToArray());

            if (_defaultTimeout.HasValue)
                config.DefaultTimeout = _defaultTimeout.Value;

            return new BytesOcean(config);
        }
    }
}
