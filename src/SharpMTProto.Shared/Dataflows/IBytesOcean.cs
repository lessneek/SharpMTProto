//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Dataflows
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBytesOcean
    {
        int Size { get; }
        int MaximalBucketSize { get; }
        int MinimalBucketSize { get; }
        Task<IBytesBucket> TakeAsync(int minimalSize, CancellationToken cancellationToken);
    }

    public static class BytesOceanExtensions
    {
        public static Task<IBytesBucket> TakeAsync(this IBytesOcean bytesOcean, int minimalSize)
        {
            return bytesOcean.TakeAsync(minimalSize, CancellationToken.None);
        }
    }
}
