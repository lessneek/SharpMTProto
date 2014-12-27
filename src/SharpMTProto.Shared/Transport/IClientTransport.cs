// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IClientTransport.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    using System;
    using System.Reactive.Disposables;
    using System.Threading;
    using System.Threading.Tasks;
    using Dataflows;

    public enum ClientTransportState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3
    }

    /// <summary>
    ///     Transport connect result.
    /// </summary>
    public enum TransportConnectResult
    {
        Unknown = 0,
        Success = 1,
        Fail = 2,
        Timeout = 3
    }

    public interface IClientTransport : IObservable<IBytesBucket>, ICancelable
    {
        bool IsConnected { get; }
        ClientTransportState State { get; }
        TimeSpan SendingTimeout { get; set; }
        TimeSpan ConnectTimeout { get; set; }
        IObservable<ClientTransportState> StateChanges { get; }
        Task<TransportConnectResult> ConnectAsync();
        Task DisconnectAsync();
        Task SendAsync(IBytesBucket payload, CancellationToken cancellationToken = default (CancellationToken));
    }
}
