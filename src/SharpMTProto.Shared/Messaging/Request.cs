// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Request.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpMTProto.Annotations;

    public interface IRequest
    {
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
        ulong Send();
        void SetException(Exception ex);
        Type ResponseType { get; }
        bool IsRpc { get; }
        ulong MsgId { get; }
    }

    public class Request<TResponse> : IRequest
    {
        private static readonly Type ResponseTypeInternal = typeof (TResponse);
        private readonly Func<object, ulong> _send;
        private readonly TaskCompletionSource<TResponse> _taskCompletionSource = new TaskCompletionSource<TResponse>();
        private readonly object _messageBody;
        private ulong _msgId;
        private readonly IRequestsManager _requestsManager;

        public Request([NotNull] object messageBody,
            MessageSendingFlags flags,
            [NotNull] Func<object, ulong> send,
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

        public ulong MsgId
        {
            get { return _msgId; }
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

        public ulong Send()
        {
            var oldMsgId = _msgId;
            
            // Sending.
            var newMsgId = _send(_messageBody);
            _msgId = newMsgId;
            
            if (oldMsgId != default(ulong))
            {
                _requestsManager.Change(newMsgId, oldMsgId);
            }
            else
            {
                _requestsManager.Add(this);
            }

            return _msgId;
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
