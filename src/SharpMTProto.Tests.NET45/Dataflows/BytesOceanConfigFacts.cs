//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Dataflows
{
    using System;
    using FluentAssertions;
    using NUnit.Framework;
    using SharpMTProto.Dataflows;

    public class BytesOceanConfigFacts
    {
        [Test]
        public void Should_be_created_with_constructor()
        {
            var config = new BytesOceanConfig(new BytesBucketsConfig(1024, 1024), new BytesBucketsConfig(16, 10000));

            config.BucketsConfigs.Should().HaveCount(2);
            config.BucketSizes.Should().Be(2);
            config.TotalSize.Should().Be(1024*1024 + 16*10000);
        }
    }

    public class BytesBucketsConfigTests
    {
        [Test]
        [TestCase(3, 42)]
        [TestCase(9, 82)]
        [TestCase(16, 12)]
        [TestCase(0, 3, ExpectedException = typeof (ArgumentOutOfRangeException))]
        [TestCase(3, 0, ExpectedException = typeof (ArgumentOutOfRangeException))]
        public void Should_be_created_with_constructor(int count, int size)
        {
            var bucketsConfig = new BytesBucketsConfig(count, size);
            bucketsConfig.Count.Should().Be(count);
            bucketsConfig.BucketSize.Should().Be(size);
            bucketsConfig.TotalSize.Should().Be(count*size);
        }
    }
}
