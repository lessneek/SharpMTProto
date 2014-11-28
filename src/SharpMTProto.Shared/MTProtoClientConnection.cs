// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using Catel;
    using Catel.Collections;
    using Catel.Logging;
    using Messaging;
    using Messaging.Handlers;
    using Schema;
    using Services;
    using SharpTL;
    using Transport;

    // ReSharper disable ClassWithVirtualMembersNeverInherited.Global

    /// <summary>
    ///     MTProto connection state.
    /// </summary>
    public enum MTProtoConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2
    }

    /// <summary>
    ///     MTProto connect result.
    /// </summary>
    public enum MTProtoConnectResult
    {
        Success,
        Timeout,
        Other
    }

    /// <summary>
    ///     Interface of a client MTProto connection.
    /// </summary>
    public interface IMTProtoClientConnection : IMTProtoConnection, IDisposable, IRemoteProcedureCaller
    {
        /// <summary>
        ///     Sends request and wait for a response asynchronously.
        /// </summary>
        /// <typeparam name="TResponse">Type of a response.</typeparam>
        /// <param name="requestBody">Request body.</param>
        /// <param name="flags">Request message sending flags.</param>
        /// <returns>Response.</returns>
        Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags);

        /// <summary>
        ///     Sends request and wait for a response asynchronously.
        /// </summary>
        /// <typeparam name="TResponse">Type of a response.</typeparam>
        /// <param name="requestBody">Request body.</param>
        /// <param name="flags">Request message sending flags.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout);

        /// <summary>
        ///     Sends request and wait for a response asynchronously.
        /// </summary>
        /// <typeparam name="TResponse">Type of a response.</typeparam>
        /// <param name="requestBody">Request body.</param>
        /// <param name="flags">Request message sending flags.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response.</returns>
        Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, CancellationToken cancellationToken);

        /// <summary>
        ///     Sends request and wait for a response asynchronously.
        /// </summary>
        /// <typeparam name="TResponse">Type of a response.</typeparam>
        /// <param name="requestBody">Request body.</param>
        /// <param name="flags">Request message sending flags.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response.</returns>
        Task<TResponse> RequestAsync<TResponse>(object requestBody,
            MessageSendingFlags flags,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        IMTProtoAsyncMethods Methods { get; }
    }

    /// <summary>
    ///     Client MTProto connection.
    /// </summary>
    public class MTProtoClientConnection : MTProtoConnection, IMTProtoClientConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<Type, MessageSendingFlags> _messageSendingFlags = new Dictionary<Type, MessageSendingFlags>();

        private readonly MTProtoAsyncMethods _methods;
        private readonly RequestsManager _requestsManager = new RequestsManager();

        public MTProtoClientConnection([NotNull] IClientTransport clientTransport,
            [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMessageCodec messageCodec) : base(clientTransport, tlRig, messageIdGenerator, messageCodec)
        {
            _methods = new MTProtoAsyncMethods(this);

            InitMessageDispatcher();
        }

        public IMTProtoAsyncMethods Methods
        {
            get { return _methods; }
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags)
        {
            return RequestAsync<TResponse>(requestBody, flags, DefaultSendingTimeout, CancellationToken.None);
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout)
        {
            return RequestAsync<TResponse>(requestBody, flags, timeout, CancellationToken.None);
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return RequestAsync<TResponse>(requestBody, flags, DefaultSendingTimeout, cancellationToken);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody,
            MessageSendingFlags flags,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Argument.IsNotNull(() => requestBody);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDiconnected();

            var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using (
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token,
                    cancellationToken,
                    ConnectionCts.Token))
            {
                Request<TResponse> request = CreateRequest<TResponse>(requestBody, flags, cts.Token);
                Log.Info("Sending request ({0}) '{1}'.", flags, requestBody);
                await request.SendAsync();
                return await request.GetResponseAsync();
            }
        }

        public Task<TResponse> RpcAsync<TResponse>(object requestBody)
        {
            return RequestAsync<TResponse>(requestBody, GetMessageSendingFlags(requestBody));
        }

        public Task SendAsync(object requestBody)
        {
            return SendAsync(requestBody, GetMessageSendingFlags(requestBody), DefaultSendingTimeout, CancellationToken.None);
        }

        public void SetMessageSendingFlags(Dictionary<Type, MessageSendingFlags> flags)
        {
            _messageSendingFlags.AddRange(flags);
        }

        private void InitMessageDispatcher()
        {
            IMessageDispatcher messageDispatcher = MessageDispatcher;
            messageDispatcher.FallbackHandler = new FirstRequestResponseHandler(_requestsManager);
            messageDispatcher.AddHandler(new BadMsgNotificationHandler(this, _requestsManager));
            messageDispatcher.AddHandler(new MessageContainerHandler(messageDispatcher));
            messageDispatcher.AddHandler(new RpcResultHandler(_requestsManager));
            messageDispatcher.AddHandler(new SessionHandler());
        }

        private Task SendRequestAsync(IRequest request, CancellationToken cancellationToken)
        {
            return SendAsync(request.Message, request.Flags, cancellationToken);
        }

        private Request<TResponse> CreateRequest<TResponse>(object body, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            var request = new Request<TResponse>(CreateMessage(body, flags.HasFlag(MessageSendingFlags.ContentRelated)),
                flags,
                SendRequestAsync,
                cancellationToken);
            _requestsManager.Add(request);
            return request;
        }

        private MessageSendingFlags GetMessageSendingFlags(object requestBody,
            MessageSendingFlags defaultSendingFlags = MessageSendingFlags.EncryptedAndContentRelatedRPC)
        {
            MessageSendingFlags flags;
            Type requestBodyType = requestBody.GetType();

            if (!_messageSendingFlags.TryGetValue(requestBodyType, out flags))
            {
                flags = defaultSendingFlags;
            }
            return flags;
        }
    }
}
