//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using BigMath.Utils;
    using Dataflows;
    using SharpTL;

    public class TcpTransportFullPacketProcessor : TransportPacketProcessorBase
    {
        private uint? _currentPacketCrc;
        private int _nextPacketBytesCountLeft;
        private IBytesBucket _nextPacketDataBucket;
        private TLCrcStreamer _nextPacketStreamer;
        private int _packetNumber = -1;
        private int _tempLengthBufferFill;
        private readonly byte[] _tempLengthBuffer = new byte[TcpFullTransportPacketLengthBytesCount];

        public TcpTransportFullPacketProcessor(IBytesOcean bytesOcean = null) : base(bytesOcean)
        {
        }

        /// <summary>
        ///     When bytes left to read is 0, then there is no currently processing packet.
        /// </summary>
        public override bool IsProcessingIncomingPacket
        {
            get { return _nextPacketBytesCountLeft > 0; }
        }

        public override int PacketEmbracesLength
        {
            get { return TcpFullTransportPacketEmbracesLength; }
        }

        public override Task<int> WritePacketAsync(ArraySegment<byte> payload,
            TLStreamer streamer,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Func<int> func = () =>
            {
                int length = payload.Count + PacketEmbracesLength;
                if (length > streamer.BytesTillEnd)
                {
                    throw new TransportException("Buffer has not free space to write a TCP packet.");
                }
                int packetNumber = Interlocked.Increment(ref _packetNumber);
                using (var crcStreamer = new TLCrcStreamer(streamer, true))
                {
                    crcStreamer.WriteInt32(length);
                    crcStreamer.WriteInt32(packetNumber);
                    crcStreamer.Write(payload);
                    crcStreamer.WriteUInt32(crcStreamer.WriteCrc);
                }
                return length;
            };
            return Task.Run(func, cancellationToken);
        }

        public override Task ProcessIncomingPacketAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            Action action = async () =>
            {
                ThrowIfDisposed();
                try
                {
                    var bytesRead = 0;
                    while (bytesRead < buffer.Count)
                    {
                        int startIndex = buffer.Offset + bytesRead;
                        int bytesToRead = buffer.Count - bytesRead;

                        if (!IsProcessingIncomingPacket)
                        {
                            #region Start next packet processing.

                            int tempLengthBytesToRead = TcpFullTransportPacketLengthBytesCount - _tempLengthBufferFill;
                            tempLengthBytesToRead = (bytesToRead < tempLengthBytesToRead) ? bytesToRead : tempLengthBytesToRead;
                            Buffer.BlockCopy(buffer.Array, startIndex, _tempLengthBuffer, _tempLengthBufferFill, tempLengthBytesToRead);
                            bytesRead += tempLengthBytesToRead;

                            _tempLengthBufferFill += tempLengthBytesToRead;
                            if (_tempLengthBufferFill < TcpFullTransportPacketLengthBytesCount)
                            {
                                // Break and wait for remaining bytes of encoded length of a packet.
                                break;
                                // TODO: if length of a buffer is always divisible by 4, so no need in this complex checks.
                            }
                            _tempLengthBufferFill = 0;

                            startIndex += tempLengthBytesToRead;
                            bytesToRead -= tempLengthBytesToRead;

                            // Reading expected packet length.
                            _nextPacketBytesCountLeft = _tempLengthBuffer.ToInt32();
                            // TODO: check packet length is divisible by 4.
                            if (_nextPacketBytesCountLeft <= 0)
                            {
                                // Empty packet.
                                throw new TransportException(string.Format("Packet with zero length received. Processing buffer: {0}.",
                                    buffer.ToHexString()));
                            }

                            _nextPacketDataBucket = await BytesOcean.TakeAsync(_nextPacketBytesCountLeft, cancellationToken).ConfigureAwait(false);
                            _nextPacketDataBucket.Used = _nextPacketBytesCountLeft;
                            _nextPacketStreamer = new TLCrcStreamer(_nextPacketDataBucket.Bytes);

                            // Writing packet length.
                            _nextPacketStreamer.Write(_tempLengthBuffer);
                            _nextPacketBytesCountLeft -= TcpFullTransportPacketLengthBytesCount;

                            #endregion
                        }

                        Debug.Assert(_nextPacketStreamer != null);

                        bytesToRead = bytesToRead > _nextPacketBytesCountLeft ? _nextPacketBytesCountLeft : bytesToRead;
                        _nextPacketBytesCountLeft -= bytesToRead;

                        if (_nextPacketBytesCountLeft < TcpFullTransportPacketCrcBytesCount && !_currentPacketCrc.HasValue)
                        {
                            #region All data for CRC is received, hence read it.

                            int crcPartToRead = TcpFullTransportPacketCrcBytesCount - _nextPacketBytesCountLeft;
                            bytesToRead = bytesToRead - crcPartToRead;

                            // Write remaining data part and save a CRC.
                            _nextPacketStreamer.Write(buffer.Array, startIndex, bytesToRead);
                            _currentPacketCrc = _nextPacketStreamer.WriteCrc;

                            bytesRead += bytesToRead;
                            startIndex = buffer.Offset + bytesRead;
                            bytesToRead = crcPartToRead;

                            #endregion
                        }

                        await _nextPacketStreamer.WriteAsync(buffer.Array, startIndex, bytesToRead, cancellationToken).ConfigureAwait(false);
                        bytesRead += bytesToRead;

                        if (IsProcessingIncomingPacket)
                        {
                            // Not all data was read for currently receiving packet, hence break and wait for next part of the data.
                            break;
                        }

                        // All data for current packet is written to the streamer.

                        #region Check CRC.

                        _nextPacketStreamer.Seek(-4, SeekOrigin.Current);
                        uint expectedCrc = _nextPacketStreamer.ReadUInt32();
                        if (_currentPacketCrc != expectedCrc)
                        {
                            throw new TransportException(string.Format("Invalid packet CRC32. Expected: {0}, actual: {1}.",
                                expectedCrc,
                                _currentPacketCrc));
                        }

                        #endregion

                        // Push payload.
                        _nextPacketDataBucket.Used -= PacketEmbracesLength;
                        _nextPacketDataBucket.Offset = TcpFullTransportPacketHeaderLength;
                        MessageBuckets.OnNext(_nextPacketDataBucket);

                        Cleanup(false, false);
                    }
                }
                catch (Exception)
                {
                    Cleanup(true, true);
                    throw;
                }
            };
            return Task.Run(action, cancellationToken);
        }

        protected override void Cleanup(bool disposing, bool reseting)
        {
            _currentPacketCrc = null;

            if (_nextPacketStreamer != null)
            {
                _nextPacketStreamer.Dispose();
                _nextPacketStreamer = null;
            }

            if (_nextPacketDataBucket != null)
            {
                if (disposing || reseting)
                {
                    _nextPacketDataBucket.Dispose();
                }
                _nextPacketDataBucket = null;
            }

            _nextPacketBytesCountLeft = 0;

            if (disposing)
            {
                if (MessageBuckets != null)
                {
                    MessageBuckets.Dispose();
                    MessageBuckets = null;
                }
            }
        }

        #region Constants

        private const int TcpFullTransportPacketLengthBytesCount = 4;
        private const int TcpFullTransportPacketNumberBytesCount = 4;
        private const int TcpFullTransportPacketCrcBytesCount = 4;

        /// <summary>
        ///     <see cref="TcpFullTransportPacketLengthBytesCount" /> + <see cref="TcpFullTransportPacketNumberBytesCount" />
        /// </summary>
        private const int TcpFullTransportPacketHeaderLength = TcpFullTransportPacketLengthBytesCount + TcpFullTransportPacketNumberBytesCount;

        /// <summary>
        ///     <see cref="TcpFullTransportPacketHeaderLength" /> + <see cref="TcpFullTransportPacketCrcBytesCount" />
        /// </summary>
        private const int TcpFullTransportPacketEmbracesLength = TcpFullTransportPacketHeaderLength + TcpFullTransportPacketCrcBytesCount;

        #endregion
    }
}
