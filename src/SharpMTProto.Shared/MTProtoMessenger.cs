//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto
{
    using System;
    using System.Diagnostics;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using BigMath.Utils;
    using SharpMTProto.Annotations;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;
    using SharpTL;

    /// <summary>
    ///     Interface of a MTProto connection.
    /// </summary>
    public interface IMTProtoMessenger : ICancelable
    {
        IMessageProducer IncomingMessages { get; }
        void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly);
        Task SendAsync(IMessageEnvelope messageEnvelope, CancellationToken cancellationToken);
        IObservable<IBytesBucket> OutgoingMessageBytesBuckets { get; }

        /// <summary>
        ///     Processes incoming message bytes asynchronously.
        /// </summary>
        /// <param name="messageBucket">Incoming bytes in a bucket.</param>
        Task ProcessIncomingMessageBytesAsync(IBytesBucket messageBucket);
    }

    public static class MTProtoMessengerExtensions
    {
        public static Task SendAsync(this IMTProtoMessenger messenger, IMessageEnvelope messageEnvelope)
        {
            return messenger.SendAsync(messageEnvelope, CancellationToken.None);
        }
    }

    public class MTProtoMessenger : Cancelable, IMTProtoMessenger
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IBytesOcean _bytesOcean;
        private readonly IMessageCodec _messageCodec;

        private readonly MessageCodecMode _incomingMessageCodecMode;
        private readonly MessageCodecMode _outgoingMessageCodecMode;

        private Subject<IMessageEnvelope> _incomingMessages = new Subject<IMessageEnvelope>();
        private IMessageProducer _incomingMessagesProducer;

        private Subject<IBytesBucket> _outgoingMessageBytesBuckets = new Subject<IBytesBucket>();
        private IObservable<IBytesBucket> _outgoingMessageBytesBucketsAsObservable;

        public MTProtoMessenger([NotNull] IMessageCodec messageCodec,
            IBytesOcean bytesOcean = null,
            MessageCodecMode outgoingMessageCodecMode = MessageCodecMode.Client)
        {
            if (messageCodec == null)
                throw new ArgumentNullException("messageCodec");

            _messageCodec = messageCodec;
            _outgoingMessageCodecMode = outgoingMessageCodecMode;
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultMTProtoMessengerBytesOcean();

            _incomingMessageCodecMode = _outgoingMessageCodecMode == MessageCodecMode.Server ? MessageCodecMode.Client : MessageCodecMode.Server;
        }

        public IMessageProducer IncomingMessages
        {
            get { return _incomingMessagesProducer ?? (_incomingMessagesProducer = _incomingMessages.AsMessageProducer()); }
        }

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            _messageCodec.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        public Task SendAsync(IMessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                IBytesBucket messageBytesBucket = await _bytesOcean.TakeAsync(MTProtoDefaults.MaximumMessageLength);
                using (var streamer = new TLStreamer(messageBytesBucket.Bytes))
                {
                    await _messageCodec.EncodeMessageAsync(messageEnvelope, streamer, _outgoingMessageCodecMode);
                    messageBytesBucket.Used = (int) streamer.Position;
                }
                SendMessageBytesBucket(messageBytesBucket);
            },
                cancellationToken);
        }

        public IObservable<IBytesBucket> OutgoingMessageBytesBuckets
        {
            get
            {
                return _outgoingMessageBytesBucketsAsObservable ??
                    (_outgoingMessageBytesBucketsAsObservable = _outgoingMessageBytesBuckets.AsObservable());
            }
        }

        private void SendMessageBytesBucket(IBytesBucket dataBucket)
        {
            if (IsDisposed)
                return;

            LogMessageInOut(dataBucket, "OUT");
            _outgoingMessageBytesBuckets.OnNext(dataBucket);
        }

        private static void LogMessageInOut(IBytesBucket messageBytes, string inOrOut)
        {
            ArraySegment<byte> bytes = messageBytes.UsedBytes;
            Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, bytes.Count, bytes.ToHexString()));
        }

        /// <summary>
        ///     Processes incoming message bytes asynchronously.
        /// </summary>
        /// <param name="messageBucket">Incoming bytes in a bucket.</param>
        public async Task ProcessIncomingMessageBytesAsync(IBytesBucket messageBucket)
        {
            if (IsDisposed)
                return;

            try
            {
                Debug("Processing incoming message.");

                IMessageEnvelope messageEnvelope;

                using (messageBucket)
                {
                    messageEnvelope = await _messageCodec.DecodeMessageAsync(messageBucket.UsedBytes, _incomingMessageCodecMode);
                }

                _incomingMessages.OnNext(messageEnvelope);
            }
            catch (MTProtoException e)
            {
                Debug(e.Message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to receive a message.");
            }
        }

        [Conditional("DEBUG")]
        private static void Debug(string message)
        {
            Log.Debug(string.Format("[MTProtoMessenger] : {0}", message));
        }

        #region Disposable

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_incomingMessages != null)
                {
                    _incomingMessages.OnCompleted();
                    _incomingMessages.Dispose();
                    _incomingMessages = null;
                }
                if (_outgoingMessageBytesBuckets != null)
                {
                    _outgoingMessageBytesBuckets.OnCompleted();
                    _outgoingMessageBytesBuckets.Dispose();
                    _outgoingMessageBytesBuckets = null;
                }
            }
            base.Dispose(isDisposing);
        }

        #endregion
    }
}
