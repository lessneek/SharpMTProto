// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Request.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Schema;
    using SharpMTProto.Annotations;
    using SharpMTProto.Utils;

    public interface IRequest
    {
        IMessageEnvelope MessageEnvelope { get; }
        MessageSendingFlags Flags { get; }
        bool IsAcknowledged { get; }

        bool IsResponseReceived { get; }

        /// <summary>
        ///     Acknowledge UTC date time.
        /// </summary>
        DateTime? AcknowledgeTime { get; }

        /// <summary>
        ///     Response UTC date time.
        /// </summary>
        DateTime? ResponseTime { get; }

        void Acknowledge();

        void SetResponse(object response);

        bool CanSetResponse(Type responseType);
        IMessageEnvelope Send();
        void SetException(Exception ex);
        Type ResponseType { get; }
        bool IsRpc { get; }
    }

    public class Request<TResponse> : IRequest
    {
        private static readonly Type ResponseTypeInternal = typeof (TResponse);
        private readonly Func<object, IMessageEnvelope> _send;
        private readonly TaskCompletionSource<TResponse> _taskCompletionSource = new TaskCompletionSource<TResponse>();
        private readonly object _messageBody;
        private IMessageEnvelope _messageEnvelope;
        private IRequestsManager _requestsManager;

        public Request([NotNull] object messageBody,
            MessageSendingFlags flags,
            [NotNull] Func<object, IMessageEnvelope> send,
            [NotNull] IRequestsManager requestsManager,
            CancellationToken cancellationToken)
        {
            if (messageBody == null)
                throw new ArgumentNullException("messageBody");
            if (send == null)
                throw new ArgumentNullException("send");
            if (requestsManager == null)
                throw new ArgumentNullException("requestsManager");
            _messageBody = messageBody;
            _send = send;
            _requestsManager = requestsManager;
            Flags = flags;
            cancellationToken.Register(() => _taskCompletionSource.TrySetCanceled());
        }

        public DateTime? ResponseTime { get; private set; }

        public IMessageEnvelope MessageEnvelope
        {
            get { return _messageEnvelope; }
        }

        public MessageSendingFlags Flags { get; private set; }

        public bool IsRpc {get { return Flags.HasFlag(MessageSendingFlags.RPC); }}

        public bool IsAcknowledged { get; private set; }

        public bool IsResponseReceived { get { return ResponseTime.HasValue; } }

        /// <summary>
        ///     Acknowledge UTC date time.
        /// </summary>
        public DateTime? AcknowledgeTime { get; private set; }

        public void Acknowledge()
        {
            if (IsAcknowledged)
            {
                return;
            }
            IsAcknowledged = true;
            AcknowledgeTime = DateTime.UtcNow;
        }

        public void SetResponse(object response)
        {
            if (!CanSetResponse(response.GetType()))
            {
                throw new MTProtoException(string.Format("Wrong response type {0}. Expected: {1}.",
                    response.GetType(),
                    ResponseTypeInternal.Name));
            }

            Acknowledge();
            ResponseTime = DateTime.UtcNow;
            _taskCompletionSource.TrySetResult((TResponse) response);
        }

        public bool CanSetResponse(Type responseType)
        {
            return !IsResponseReceived && ResponseTypeInternal.GetTypeInfo().IsAssignableFrom(responseType.GetTypeInfo());
        }

        public IMessageEnvelope Send()
        {
            var oldMessageEnvelope = _messageEnvelope;
            var newMessageEnvelope = _send(_messageBody);
            _messageEnvelope = newMessageEnvelope;
            if (oldMessageEnvelope != null)
            {
                // Resending.
                _requestsManager.Change(newMessageEnvelope.Message.MsgId, oldMessageEnvelope.Message.MsgId);
            }
            else
            {
                _requestsManager.Add(this);
            }

            return _messageEnvelope;
        }

        public void SetException(Exception ex)
        {
            Acknowledge();
            ResponseTime = DateTime.UtcNow;

            _taskCompletionSource.TrySetException(ex);
        }

        public Type ResponseType
        {
            get { return ResponseTypeInternal; }
        }

        public Task<TResponse> GetResponseAsync()
        {
            return _taskCompletionSource.Task;
        }
    }
}
