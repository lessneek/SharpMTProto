// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IClientTransportConfig.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    using System;

    public interface IClientTransportConfig
    {
        string TransportName { get; }

        TimeSpan ConnectTimeout { get; set; }

        TimeSpan SendingTimeout { get; set; }
    }
}
