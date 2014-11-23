// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TransportFactory.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace SharpMTProto.Transport
{
    public class TransportFactory : ITransportFactory
    {
        public IClientTransport CreateTransport(IClientTransportConfig clientTransportConfig)
        {
            // TCP.
            var tcpTransportConfig = clientTransportConfig as TcpClientTransportConfig;
            if (tcpTransportConfig != null)
            {
                return new TcpClientTransport(tcpTransportConfig);
            }

            throw new NotSupportedException(string.Format("Transport type '{0}' is not supported.", clientTransportConfig.TransportName));
        }
    }
}
