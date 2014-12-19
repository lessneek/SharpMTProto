// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Defaults.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Diagnostics;

    public static class Defaults
    {
        public const int MaximumMessageLength = 1024*512;
        public static readonly TimeSpan SendingTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

        static Defaults()
        {
            if (Debugger.IsAttached)
            {
                SendingTimeout = TimeSpan.FromMinutes(9);
                ConnectTimeout = TimeSpan.FromMinutes(9);
                ResponseTimeout = TimeSpan.FromMinutes(9);
            }
        }
    }
}
