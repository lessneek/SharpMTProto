// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoDefaults.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Diagnostics;
    using Dataflows;

    public static class MTProtoDefaults
    {
        public const int MaximumMessageLength = 1024*512;
        public static readonly TimeSpan SendingTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

        static MTProtoDefaults()
        {
            if (Debugger.IsAttached)
            {
                SendingTimeout = TimeSpan.FromMinutes(9);
                ConnectTimeout = TimeSpan.FromMinutes(9);
                ResponseTimeout = TimeSpan.FromMinutes(9);
            }
        }

        public static IBytesOcean CreateDefaultTransportBytesOcean()
        {
            return BytesOcean.WithBuckets(10, MaximumMessageLength).WithBuckets(1000, MaximumMessageLength / 100).Build();
        }

        public static IBytesOcean CreateDefaultMessageCodecBytesOcean()
        {
            return BytesOcean.WithBuckets(10, MaximumMessageLength).WithBuckets(1000, MaximumMessageLength / 100).Build();
        }

        public static IBytesOcean CreateDefaultMTProtoMessengerBytesOcean()
        {
            return BytesOcean.WithBuckets(10, MaximumMessageLength).WithBuckets(1000, MaximumMessageLength / 100).Build();
        }

        public static IBytesOcean CreateDefaultTcpTransportPacketProcessorBytesOcean()
        {
            return BytesOcean.WithBuckets(10, MaximumMessageLength).WithBuckets(1000, MaximumMessageLength / 100).Build();
        }
    }
}
