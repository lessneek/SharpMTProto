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
    using SharpMTProto.Dataflows;
    using SharpMTProto.Utils;

    /// <summary>
    ///     Clien transport state.
    /// </summary>
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

    /// <summary>
    ///     A client transport without ability to connect. May be used as connected transport.
    /// </summary>
    public interface IClientTransport : IObservable<IBytesBucket>, ICancelable
    {
        Guid TransportId { get; }
        IObservableReadonlyProperty<IClientTransport, ClientTransportState> State { get; }
        bool IsConnected { get; }
        TimeSpan SendingTimeout { get; set; }
        Task SendAsync(IBytesBucket payload, CancellationToken cancellationToken = default (CancellationToken));
        Task DisconnectAsync();
    }

    /// <summary>
    ///     Connectable client transport.
    /// </summary>
    public interface IConnectableClientTransport : IClientTransport
    {
        TimeSpan ConnectTimeout { get; set; }
        Task<TransportConnectResult> ConnectAsync();
    }
}
