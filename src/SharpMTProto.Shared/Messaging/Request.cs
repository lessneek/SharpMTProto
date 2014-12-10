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
    using Schema;

    public interface IRequest
    {
        IMessage Message { get; }
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
        Task SendAsync();
        void SetException(Exception ex);
        Type ResponseType { get; }
        bool IsRpc { get; }
    }

    public class Request<TResponse> : IRequest
    {
        private static readonly Type ResponseTypeInternal = typeof (TResponse);
        private readonly CancellationToken _cancellationToken;
        private readonly Func<IRequest, CancellationToken, Task> _sendAsync;
        private readonly TaskCompletionSource<TResponse> _taskCompletionSource = new TaskCompletionSource<TResponse>();

        public Request(IMessage message,
            MessageSendingFlags flags,
            Func<IRequest, CancellationToken, Task> sendAsync,
            CancellationToken cancellationToken)
        {
            _sendAsync = sendAsync;
            _cancellationToken = cancellationToken;
            Message = message;
            Flags = flags;
            cancellationToken.Register(() => _taskCompletionSource.TrySetCanceled());
        }

        public DateTime? ResponseTime { get; private set; }

        public IMessage Message { get; private set; }

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

        public Task SendAsync()
        {
            return _sendAsync(this, _cancellationToken);
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
