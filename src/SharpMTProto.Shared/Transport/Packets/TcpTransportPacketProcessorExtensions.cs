//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport.Packets
{
    using System;
    using SharpTL;

    public static class TcpTransportPacketProcessorExtensions
    {
        /// <summary>
        ///     Writes TCP packet.
        /// </summary>
        /// <param name="packetProcessor">TCP packet processor.</param>
        /// <param name="packetNumber">Packet number.</param>
        /// <param name="payload">Payload.</param>
        /// <returns>TCP packet as array of bytes.</returns>
        public static byte[] WriteTcpPacket(this ITcpTransportPacketProcessor packetProcessor, int packetNumber, ArraySegment<byte> payload)
        {
            var bytes = new byte[payload.Count + packetProcessor.PacketEmbracesLength];
            using (var streamer = new TLStreamer(bytes))
            {
                packetProcessor.WriteTcpPacket(packetNumber, payload, streamer);
            }
            return bytes;
        }

        /// <summary>
        ///     Writes TCP packet.
        /// </summary>
        /// <param name="packetProcessor">TCP packet processor.</param>
        /// <param name="packetNumber">Packet number.</param>
        /// <param name="payload">Payload.</param>
        /// <returns>TCP packet as array of bytes.</returns>
        public static byte[] WriteTcpPacket(this ITcpTransportPacketProcessor packetProcessor, int packetNumber, byte[] payload)
        {
            return packetProcessor.WriteTcpPacket(packetNumber, new ArraySegment<byte>(payload));
        }
    }
}
