//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport
{
    using System;
    using System.Reactive.Disposables;
    using System.Threading;
    using System.Threading.Tasks;
    using Dataflows;
    using SharpTL;

    public interface ITransportPacketProcessor : ICancelable
    {
        /// <summary>
        ///     When bytes left to read is 0, then there is no currently processing packet.
        /// </summary>
        bool IsProcessingIncomingPacket { get; }

        /// <summary>
        ///     Packet embraces length.
        /// </summary>
        int PacketEmbracesLength { get; }

        /// <summary>
        ///     Incoming message buckets.
        /// </summary>
        IObservable<IBytesBucket> IncomingMessageBuckets { get; }

        /// <summary>
        ///     Processes incoming packet.
        /// </summary>
        /// <param name="buffer">A buffer with packet bytes.</param>
        /// <param name="cancellationToken"></param>
        Task ProcessIncomingPacketAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default (CancellationToken));

        /// <summary>
        ///     Writes packet with a payload.
        /// </summary>
        /// <param name="payload">Payload bytes.</param>
        /// <param name="streamer">Streamer to write.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Count of written bytes.</returns>
        Task<int> WritePacketAsync(ArraySegment<byte> payload, TLStreamer streamer, CancellationToken cancellationToken = default (CancellationToken));

        /// <summary>
        ///     Resets internal state as if processor just created.
        /// </summary>
        void Reset();
    }
}
