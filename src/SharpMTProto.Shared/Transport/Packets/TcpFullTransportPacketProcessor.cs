//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport.Packets
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;
    using BigMath.Utils;
    using Dataflows;
    using SharpTL;
    using Utils;

    public class TcpFullTransportPacketProcessor : Cancelable, ITcpTransportPacketProcessor
    {
        private const int PacketLengthBytesCount = 4;
        private const int PacketNumberBytesCount = 4;
        private const int PacketCrcBytesCount = 4;

        /// <summary>
        ///     <see cref="PacketLengthBytesCount" /> + <see cref="PacketNumberBytesCount" />
        /// </summary>
        private const int PacketHeaderLength = PacketLengthBytesCount + PacketNumberBytesCount;

        /// <summary>
        ///     <see cref="PacketHeaderLength" /> + <see cref="PacketCrcBytesCount" />
        /// </summary>
        private const int PacketEmbracesLength = PacketHeaderLength + PacketCrcBytesCount;

        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private uint? _currentPacketCrc;
        private Subject<IBytesBucket> _messageBytesBuckets = new Subject<IBytesBucket>();
        private int _nextPacketBytesCountLeft;
        private IBytesBucket _nextPacketDataBucket;
        private TLCrcStreamer _nextPacketStreamer;
        private int _tempLengthBufferFill;
        private readonly IBytesOcean _bytesOcean;
        private readonly byte[] _tempLengthBuffer = new byte[PacketLengthBytesCount];

        public TcpFullTransportPacketProcessor(IBytesOcean bytesOcean)
        {
            _bytesOcean = bytesOcean;
        }

        public byte[] WriteTcpPacket(int packetNumber, byte[] payload)
        {
            return WriteTcpPacket(packetNumber, new ArraySegment<byte>(payload));
        }

        public byte[] WriteTcpPacket(int packetNumber, ArraySegment<byte> payload)
        {
            var bytes = new byte[payload.Count + PacketEmbracesLength];
            using (var streamer = new TLStreamer(bytes))
            {
                WriteTcpPacket(packetNumber, payload, streamer);
            }
            return bytes;
        }

        public int WriteTcpPacket(int packetNumber, ArraySegment<byte> payload, TLStreamer streamer)
        {
            int length = payload.Count + PacketEmbracesLength;
            if (length > streamer.BytesTillEnd)
            {
                throw new TransportException("Buffer has not free space to write a TCP packet.");
            }
            using (var crcStreamer = new TLCrcStreamer(streamer, true))
            {
                crcStreamer.WriteInt32(length);
                crcStreamer.WriteInt32(packetNumber);
                crcStreamer.Write(payload);
                crcStreamer.WriteUInt32(crcStreamer.WriteCrc);
            }
            return length;
        }

        /// <summary>
        ///     When bytes left to read is 0, then there is no currently processing packet.
        /// </summary>
        public bool IsProcessingPacket
        {
            get { return _nextPacketBytesCountLeft > 0; }
        }

        public IDisposable Subscribe(IObserver<IBytesBucket> observer)
        {
            return _messageBytesBuckets.Subscribe(observer);
        }

        public async Task ProcessPacketAsync(ArraySegment<byte> buffer)
        {
            ThrowIfDisposed();
            try
            {
                var bytesRead = 0;
                while (bytesRead < buffer.Count)
                {
                    int startIndex = buffer.Offset + bytesRead;
                    int bytesToRead = buffer.Count - bytesRead;

                    if (!IsProcessingPacket)
                    {
                        #region Start next packet processing.

                        int tempLengthBytesToRead = PacketLengthBytesCount - _tempLengthBufferFill;
                        tempLengthBytesToRead = (bytesToRead < tempLengthBytesToRead) ? bytesToRead : tempLengthBytesToRead;
                        Buffer.BlockCopy(buffer.Array, startIndex, _tempLengthBuffer, _tempLengthBufferFill, tempLengthBytesToRead);
                        bytesRead += tempLengthBytesToRead;

                        _tempLengthBufferFill += tempLengthBytesToRead;
                        if (_tempLengthBufferFill < PacketLengthBytesCount)
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
                        if (_nextPacketBytesCountLeft <= 0)
                        {
                            // Empty packet.
                            throw new TransportException(string.Format("Packet with zero length received. Processing buffer: {0}.",
                                buffer.ToHexString()));
                        }

                        _nextPacketDataBucket = await _bytesOcean.TakeAsync(_nextPacketBytesCountLeft);
                        _nextPacketDataBucket.Used = _nextPacketBytesCountLeft;
                        _nextPacketStreamer = new TLCrcStreamer(_nextPacketDataBucket.Bytes);

                        // Writing packet length.
                        _nextPacketStreamer.Write(_tempLengthBuffer);
                        _nextPacketBytesCountLeft -= PacketLengthBytesCount;

                        #endregion
                    }

                    Debug.Assert(_nextPacketStreamer != null);

                    bytesToRead = bytesToRead > _nextPacketBytesCountLeft ? _nextPacketBytesCountLeft : bytesToRead;
                    _nextPacketBytesCountLeft -= bytesToRead;

                    if (_nextPacketBytesCountLeft < PacketCrcBytesCount && !_currentPacketCrc.HasValue)
                    {
                        #region All data for CRC is received, hence read it.

                        int crcPartToRead = PacketCrcBytesCount - _nextPacketBytesCountLeft;
                        bytesToRead = bytesToRead - crcPartToRead;

                        // Write remaining data part and save a CRC.
                        _nextPacketStreamer.Write(buffer.Array, startIndex, bytesToRead);
                        _currentPacketCrc = _nextPacketStreamer.WriteCrc;

                        bytesRead += bytesToRead;
                        startIndex = buffer.Offset + bytesRead;
                        bytesToRead = crcPartToRead;

                        #endregion
                    }

                    _nextPacketStreamer.Write(buffer.Array, startIndex, bytesToRead);
                    bytesRead += bytesToRead;

                    if (IsProcessingPacket)
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
                        throw new TransportException(string.Format("Invalid packet CRC32. Expected: {0}, actual: {1}.", expectedCrc, _currentPacketCrc));
                    }

                    #endregion

                    // Push payload.
                    _nextPacketDataBucket.Used -= PacketEmbracesLength;
                    _nextPacketDataBucket.Offset = PacketHeaderLength;
                    _messageBytesBuckets.OnNext(_nextPacketDataBucket);

                    Cleanup(false);
                }
            }
            catch (Exception)
            {
                Cleanup(true);
                throw;
            }
        }

        private void Cleanup(bool disposing)
        {
            _currentPacketCrc = null;

            if (_nextPacketStreamer != null)
            {
                _nextPacketStreamer.Dispose();
                _nextPacketStreamer = null;
            }

            if (_nextPacketDataBucket != null)
            {
                if (disposing)
                {
                    _nextPacketDataBucket.Dispose();
                }
                _nextPacketDataBucket = null;
            }

            _nextPacketBytesCountLeft = 0;

            if (disposing)
            {
                if (_messageBytesBuckets != null)
                {
                    _messageBytesBuckets.Dispose();
                    _messageBytesBuckets = null;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup(true);
            }
            base.Dispose(disposing);
        }
    }
}
