//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Transport
{
    using System;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;
    using Dataflows;
    using SharpTL;
    using Utils;

    public abstract class TransportPacketProcessorBase : Cancelable, ITransportPacketProcessor
    {
        protected Subject<IBytesBucket> MessageBuckets = new Subject<IBytesBucket>();
        protected readonly IBytesOcean BytesOcean;
        protected readonly ILog Log = LogManager.GetCurrentClassLogger();

        protected TransportPacketProcessorBase(IBytesOcean bytesOcean = null)
        {
            BytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultTcpTransportPacketProcessorBytesOcean();
        }

        public IDisposable Subscribe(IObserver<IBytesBucket> observer)
        {
            return MessageBuckets.Subscribe(observer);
        }

        public abstract bool IsProcessingIncomingPacket { get; }
        public abstract int PacketEmbracesLength { get; }

        public IObservable<IBytesBucket> IncomingMessageBuckets
        {
            get { return MessageBuckets.AsObservable(); }
        }

        public abstract Task ProcessIncomingPacketAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<int> WritePacketAsync(ArraySegment<byte> payload,
            TLStreamer streamer,
            CancellationToken cancellationToken = default(CancellationToken));

        public void Reset()
        {
            Cleanup(false, true);
        }

        protected virtual void Cleanup(bool disposing, bool reseting)
        {
            if (disposing)
            {
                if (MessageBuckets != null)
                {
                    MessageBuckets.Dispose();
                    MessageBuckets = null;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup(true, false);
            }
            base.Dispose(disposing);
        }
    }
}
