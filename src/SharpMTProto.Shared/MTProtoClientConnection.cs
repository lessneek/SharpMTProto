// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientConnection.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#region R#

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

#endregion

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reactive.Disposables;
    using System.Reactive.Subjects;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Annotations;
    using Authentication;
    using Messaging;
    using Messaging.Handlers;
    using Schema;
    using Transport;
    using Utils;

    /// <summary>
    ///     Interface of a client MTProto connection.
    /// </summary>
    public interface IMTProtoClientConnection : ICancelable, IRemoteProcedureCaller
    {
        IMTProtoAsyncMethods Methods { get; }
        bool IsConnected { get; }
        IClientTransport Transport { get; }
        TimeSpan DefaultResponseTimeout { get; set; }
        IMTProtoMessenger Messenger { get; }

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
        void SetAuthInfo(AuthInfo authInfo);
        void SetSessionId(ulong sessionId);
    }

    /// <summary>
    ///     Client MTProto connection.
    /// </summary>
    public class MTProtoClientConnection : Cancelable, IMTProtoClientConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private ImmutableDictionary<Type, MessageSendingFlags> _messageSendingFlags = ImmutableDictionary<Type, MessageSendingFlags>.Empty;
        private readonly object _messageSendingFlagsSyncRoot = new object();

        private IMTProtoMessenger _messenger;

        private readonly MTProtoAsyncMethods _methods;
        private IRequestsManager _requestsManager = new RequestsManager();
        private MessageHandlersHub _messageHandlersHub;

        private BehaviorSubject<ImmutableArray<Type>> _firstRequestResponseMessageTypes =
            new BehaviorSubject<ImmutableArray<Type>>(ImmutableArray<Type>.Empty);

        public MTProtoClientConnection([NotNull] IMTProtoMessenger messenger)
        {
            if (messenger == null)
            {
                throw new ArgumentNullException("messenger");
            }

            _messenger = messenger;
            _methods = new MTProtoAsyncMethods(this);

            DefaultResponseTimeout = MTProtoDefaults.ResponseTimeout;

            InitMessageDispatcher();
        }

        public TimeSpan DefaultResponseTimeout { get; set; }

        public IMTProtoAsyncMethods Methods
        {
            get { return _methods; }
        }

        public IMTProtoMessenger Messenger
        {
            get
            {
                ThrowIfDisposed();
                return _messenger;
            }
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

        public void SetAuthInfo(AuthInfo authInfo)
        {
            ThrowIfDisposed();
            _messenger.SetAuthInfo(authInfo);
        }

        public void SetSessionId(ulong sessionId)
        {
            ThrowIfDisposed();
            _messenger.SetSessionId(sessionId);
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

        public async Task<TResponse> RequestAsync<TResponse>([NotNull] object requestBody,
            MessageSendingFlags flags,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (requestBody == null)
                throw new ArgumentNullException("requestBody");

            cancellationToken.ThrowIfCancellationRequested();

            var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using (
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token,
                    cancellationToken))
            {
                Request<TResponse> request = CreateRequest<TResponse>(requestBody, flags, cts.Token);
                Log.Debug(string.Format("Sending request ({0}) '{1}'.", flags, requestBody));
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
            return _messenger.SendAsync(requestBody, GetMessageSendingFlags(requestBody), CancellationToken.None);
        }

        public void SetMessageSendingFlags(Dictionary<Type, MessageSendingFlags> flags)
        {
            lock (_messageSendingFlagsSyncRoot)
            {
                _messageSendingFlags = _messageSendingFlags.AddRange(flags);
            }
        }

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            ThrowIfDisposed();
            _messenger.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_messenger != null)
                {
                    _messenger.Dispose();
                    _messenger = null;
                }
                if (_messageHandlersHub != null)
                {
                    _messageHandlersHub.Dispose();
                    _messageHandlersHub = null;
                }
                if (_firstRequestResponseMessageTypes != null)
                {
                    _firstRequestResponseMessageTypes.OnCompleted();
                    _firstRequestResponseMessageTypes.Dispose();
                    _firstRequestResponseMessageTypes = null;
                }
                if (_requestsManager != null)
                {
                    _requestsManager.Dispose();
                    _requestsManager = null;
                }
            }
            base.Dispose(isDisposing);
        }

        private void InitMessageDispatcher()
        {
            _messageHandlersHub = new MessageHandlersHub();
            _messageHandlersHub.Add(new MessageContainerHandler(_messageHandlersHub),
                new BadMsgNotificationHandler(_messenger, _requestsManager),
                new RpcResultHandler(_requestsManager),
                new SessionHandler(),
                new FirstRequestResponseHandler(_requestsManager, _firstRequestResponseMessageTypes));

            _messageHandlersHub.SubscribeTo(_messenger.IncomingMessages);
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

            var types = _firstRequestResponseMessageTypes.Value;
            if (!request.IsRpc && !types.Contains(request.ResponseType))
            {
                _firstRequestResponseMessageTypes.OnNext(types.Add(request.ResponseType));
            }

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
