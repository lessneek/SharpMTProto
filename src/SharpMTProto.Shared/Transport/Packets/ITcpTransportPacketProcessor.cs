//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport.Packets
{
    using System;
    using System.Reactive.Disposables;
    using System.Threading.Tasks;
    using Dataflows;
    using SharpTL;

    public interface ITcpTransportPacketProcessor : IObservable<IBytesBucket>, ICancelable
    {
        /// <summary>
        ///     When bytes left to read is 0, then there is no currently processing packet.
        /// </summary>
        bool IsProcessingPacket { get; }

        /// <summary>
        ///     Packet embraces length.
        /// </summary>
        int PacketEmbracesLength { get; }

        /// <summary>
        ///     Processes packet.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        Task ProcessPacketAsync(ArraySegment<byte> buffer);

        /// <summary>
        ///     Writes TCP packet.
        /// </summary>
        /// <param name="packetNumber">Packet number.</param>
        /// <param name="payload">Payload bytes.</param>
        /// <param name="streamer">Streamer to write.</param>
        /// <returns>Count of written bytes.</returns>
        int WriteTcpPacket(int packetNumber, ArraySegment<byte> payload, TLStreamer streamer);
    }
}
