//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Dataflows
{
    using System;
    using System.Threading.Tasks;
    using FluentAssertions;
    using NUnit.Framework;
    using SharpMTProto.Dataflows;

    public class BytesOceanFacts
    {
        [Test]
        public void Should_be_created_with_constructor()
        {
            const int size = 1024*1024 + 16*10000;

            long totalMemory = GC.GetTotalMemory(true);

            var bo = new BytesOcean(new BytesOceanConfig(new BytesBucketsConfig(1024, 1024), new BytesBucketsConfig(16, 10000)));
            bo.Size.Should().Be(size);
            bo.MinimalBucketSize.Should().Be(1024);
            bo.MaximalBucketSize.Should().Be(10000);

            long usedMemory = GC.GetTotalMemory(true) - totalMemory;
            long overheads = usedMemory - size;
            Console.WriteLine("Usable bytes: {0}. Used bytes: {1}. Overhead bytes: {2}.", size, usedMemory, overheads);
        }

        [Test]
        public void Should_be_created_with_configurator()
        {
            IBytesOcean bytesOcean = BytesOcean.WithBuckets(3, 9).WithBuckets(10, 10).Build();
            bytesOcean.Size.Should().Be(127);
            bytesOcean.MinimalBucketSize.Should().Be(9);
            bytesOcean.MaximalBucketSize.Should().Be(10);
        }

        [Test]
        [TestCase(90, 10)]
        [TestCase(100, 99)]
        [TestCase(10, 9)]
        public async Task Should_take_async_and_return_on_dispose(int minimalRequiredSize, int delta)
        {
            int minimalSize = minimalRequiredSize - delta;
            int maximalSize = minimalRequiredSize + delta;

            var ocean = new BytesOcean(new BytesOceanConfig(new BytesBucketsConfig(3, minimalSize), new BytesBucketsConfig(3, maximalSize)));
            IBytesBucket bucket = await ocean.TakeAsync(minimalRequiredSize).ConfigureAwait(false);
            bucket.Should().NotBeNull();
            bucket.Ocean.Should().BeSameAs(ocean);
            bucket.Bytes.Count.Should().Be(maximalSize);
            bucket.Dispose();
        }

        [Test]
        public void Should_throw_on_take_async_with_exceeding_minimal_size()
        {
            var ocean = new BytesOcean(new BytesOceanConfig(new BytesBucketsConfig(3, 1024), new BytesBucketsConfig(3, 16)));
            var a = new Func<Task>(async () => await ocean.TakeAsync(1025).ConfigureAwait(false));
            a.ShouldThrow<ArgumentOutOfRangeException>();
        }
    }
}
