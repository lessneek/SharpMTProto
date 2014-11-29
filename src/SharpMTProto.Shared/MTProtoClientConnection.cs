// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using Catel;
    using Catel.Collections;
    using Catel.Logging;
    using Messaging;
    using Messaging.Handlers;
    using Schema;
    using Transport;

    // ReSharper disable ClassWithVirtualMembersNeverInherited.Global

    /// <summary>
    ///     Interface of a client MTProto connection.
    /// </summary>
    public interface IMTProtoClientConnection : IDisposable, IRemoteProcedureCaller
    {
        IMTProtoAsyncMethods Methods { get; }
        bool IsConnected { get; }
        IClientTransport Transport { get; }
        TimeSpan DefaultResponseTimeout { get; set; }

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

        Task<TransportConnectResult> ConnectAsync();
        Task DisconnectAsync();
        void Configure(ConnectionConfig config);
    }

    /// <summary>
    ///     Client MTProto connection.
    /// </summary>
    public class MTProtoClientConnection : IMTProtoClientConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<Type, MessageSendingFlags> _messageSendingFlags = new Dictionary<Type, MessageSendingFlags>();
        private IMTProtoMessenger _messenger;

        private readonly MTProtoAsyncMethods _methods;
        private readonly RequestsManager _requestsManager = new RequestsManager();

        public MTProtoClientConnection([NotNull] IMTProtoMessenger messenger)
        {
            if (messenger == null)
            {
                throw new ArgumentNullException("messenger");
            }

            _messenger = messenger;
            _methods = new MTProtoAsyncMethods(this);

            DefaultResponseTimeout = Defaults.ResponseTimeout;

            InitMessageDispatcher();
        }

        public TimeSpan DefaultResponseTimeout { get; set; }

        public IMTProtoAsyncMethods Methods
        {
            get { return _methods; }
        }

        public IClientTransport Transport
        {
            get
            {
                ThrowIfDisposed();
                return _messenger.Transport;
            }
        }

        public Task<TransportConnectResult> ConnectAsync()
        {
            return Transport.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return Transport.DisconnectAsync();
        }

        public bool IsConnected
        {
            get { return Transport.IsConnected; }
        }

        public void Configure(ConnectionConfig config)
        {
            ThrowIfDisposed();
            _messenger.Configure(config);
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags)
        {
            return RequestAsync<TResponse>(requestBody, flags, DefaultResponseTimeout, CancellationToken.None);
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout)
        {
            return RequestAsync<TResponse>(requestBody, flags, timeout, CancellationToken.None);
        }

        public Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            return RequestAsync<TResponse>(requestBody, flags, DefaultResponseTimeout, cancellationToken);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object requestBody,
            MessageSendingFlags flags,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Argument.IsNotNull(() => requestBody);
            cancellationToken.ThrowIfCancellationRequested();

            var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using (
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token,
                    cancellationToken))
            {
                Request<TResponse> request = CreateRequest<TResponse>(requestBody, flags, cts.Token);
                Log.Info("Sending request ({0}) '{1}'.", flags, requestBody);
                await request.SendAsync();
                return await request.GetResponseAsync();
                // TODO: make observable timeout.
            }
        }

        public Task<TResponse> RpcAsync<TResponse>(object requestBody)
        {
            return RequestAsync<TResponse>(requestBody, GetMessageSendingFlags(requestBody));
        }

        public Task SendAsync(object requestBody)
        {
            ThrowIfDisposed();
            return _messenger.SendAsync(requestBody,
                GetMessageSendingFlags(requestBody),
                CancellationToken.None);
        }

        public void SetMessageSendingFlags(Dictionary<Type, MessageSendingFlags> flags)
        {
            _messageSendingFlags.AddRange(flags);
        }

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            ThrowIfDisposed();
            _messenger.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        #region Disposing

        private volatile bool _isDisposed;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (!isDisposing)
            {
                return;
            }

            if (_messenger != null)
            {
                _messenger.Dispose();
                _messenger = null;
            }
        }

        [DebuggerStepThrough]
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
            }
        }
        #endregion

        private void InitMessageDispatcher()
        {
            var dispatcher = _messenger.IncomingMessageDispatcher;
            dispatcher.FallbackHandler = new FirstRequestResponseHandler(_requestsManager);
            dispatcher.AddHandler(new BadMsgNotificationHandler(_messenger, _requestsManager));
            dispatcher.AddHandler(new MessageContainerHandler(dispatcher));
            dispatcher.AddHandler(new RpcResultHandler(_requestsManager));
            dispatcher.AddHandler(new SessionHandler());
        }

        private Task SendRequestAsync(IRequest request, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return _messenger.SendAsync(request.Message, request.Flags, cancellationToken);
        }

        private Request<TResponse> CreateRequest<TResponse>(object body, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var request = new Request<TResponse>(_messenger.CreateMessage(body, flags.HasFlag(MessageSendingFlags.ContentRelated)),
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
