// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientTransportFactory.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    using System;
    using Annotations;

    public class ClientTransportFactory : IClientTransportFactory
    {
        private readonly Func<TcpClientTransportConfig, IConnectableClientTransport> _createTcpClientTransport;

        public ClientTransportFactory([NotNull] Func<TcpClientTransportConfig, TcpClientTransport> createTcpClientTransport)
        {
            if (createTcpClientTransport == null)
                throw new ArgumentNullException("createTcpClientTransport");

            _createTcpClientTransport = createTcpClientTransport;
        }

        public IConnectableClientTransport CreateTransport(IClientTransportConfig clientTransportConfig)
        {
            // TCP.
            var tcpTransportConfig = clientTransportConfig as TcpClientTransportConfig;
            if (tcpTransportConfig != null)
            {
                return _createTcpClientTransport(tcpTransportConfig);
            }

            throw new NotSupportedException(string.Format("Transport type '{0}' is not supported.", clientTransportConfig.TransportName));
        }
    }
}
