//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpTL;

    public static class TransportPacketProcessorExtensions
    {
        /// <summary>
        ///     Processes incoming packet.
        /// </summary>
        /// <param name="packetProcessor">Packet processor.</param>
        /// <param name="buffer">Buffer with data to process.</param>
        public static void ProcessIncomingPacket(this ITransportPacketProcessor packetProcessor, ArraySegment<byte> buffer)
        {
            packetProcessor.ProcessIncomingPacketAsync(buffer).Wait();
        }

        /// <summary>
        ///     Writes packet.
        /// </summary>
        /// <param name="packetProcessor">Packet processor.</param>
        /// <param name="payload">Payload data.</param>
        /// <param name="streamer">Streamer.</param>
        /// <returns>Bytes written.</returns>
        public static int WritePacket(this ITransportPacketProcessor packetProcessor, ArraySegment<byte> payload, TLStreamer streamer)
        {
            return packetProcessor.WritePacketAsync(payload, streamer).Result;
        }

        /// <summary>
        ///     Writes packet.
        /// </summary>
        /// <param name="packetProcessor">Transport packet processor.</param>
        /// <param name="payload">Payload.</param>
        /// <returns>Packet as array of bytes.</returns>
        public static byte[] WritePacket(this ITransportPacketProcessor packetProcessor, ArraySegment<byte> payload)
        {
            var bytes = new byte[payload.Count + packetProcessor.PacketEmbracesLength];
            using (var streamer = new TLStreamer(bytes))
            {
                packetProcessor.WritePacketAsync(payload, streamer).Wait();
            }
            return bytes;
        }

        /// <summary>
        ///     Writes packet.
        /// </summary>
        /// <param name="packetProcessor">Transport packet processor.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Packet as array of bytes.</returns>
        public static async Task<byte[]> WritePacketAsync(this ITransportPacketProcessor packetProcessor,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = new byte[payload.Count + packetProcessor.PacketEmbracesLength];
            using (var streamer = new TLStreamer(bytes))
            {
                await packetProcessor.WritePacketAsync(payload, streamer, cancellationToken);
            }
            return bytes;
        }

        /// <summary>
        ///     Writes packet.
        /// </summary>
        /// <param name="packetProcessor">Packet processor.</param>
        /// <param name="payload">Payload.</param>
        /// <returns>Packet as array of bytes.</returns>
        public static byte[] WritePacket(this ITransportPacketProcessor packetProcessor, byte[] payload)
        {
            return packetProcessor.WritePacketAsync(new ArraySegment<byte>(payload)).Result;
        }

        /// <summary>
        ///     Writes packet.
        /// </summary>
        /// <param name="packetProcessor">Packet processor.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Packet as array of bytes.</returns>
        public static Task<byte[]> WritePacketAsync(this ITransportPacketProcessor packetProcessor,
            byte[] payload,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return packetProcessor.WritePacketAsync(new ArraySegment<byte>(payload), cancellationToken);
        }
    }
}
