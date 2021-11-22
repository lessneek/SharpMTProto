//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBytesOcean
    {
        int Size { get; }
        int MaximalBucketSize { get; }
        int MinimalBucketSize { get; }
        TimeSpan DefaultTimeout { get; set; }
        Task<IBytesBucket> TakeAsync(int minimalSize, CancellationToken cancellationToken, TimeSpan? timeout = null);
    }

    public static class BytesOceanExtensions
    {
        public static Task<IBytesBucket> TakeAsync(this IBytesOcean bytesOcean, int minimalSize, TimeSpan? timeout = null)
        {
            return bytesOcean.TakeAsync(minimalSize, CancellationToken.None, timeout);
        }
    }
}
