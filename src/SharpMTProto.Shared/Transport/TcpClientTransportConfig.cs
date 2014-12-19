// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpClientTransportConfig.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    using System;

    public class TcpClientTransportConfig : IClientTransportConfig
    {
        public TcpClientTransportConfig(string ipAddress, int port)
        {
            IPAddress = ipAddress;
            Port = port;
            ConnectTimeout = MTProtoDefaults.ConnectTimeout;
            SendingTimeout = MTProtoDefaults.SendingTimeout;
            MaxBufferSize = 2048;
        }

        public string IPAddress { get; set; }

        public int Port { get; set; }
        public int MaxBufferSize { get; set; }

        public string TransportName
        {
            get { return "TCP"; }
        }

        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan SendingTimeout { get; set; }
    }
}
