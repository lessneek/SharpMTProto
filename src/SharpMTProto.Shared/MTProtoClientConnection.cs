//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

#endregion

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reactive.Threading.Tasks;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;
    using SharpMTProto.Annotations;
    using SharpMTProto.Authentication;
    using SharpMTProto.Messaging;
    using SharpMTProto.Messaging.Handlers;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Transport;
    using SharpMTProto.Utils;

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
        Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken);

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
        private readonly object _messageSendingFlagsSyncRoot = new object();
        private readonly MTProtoAsyncMethods _methods;
        private readonly IMTProtoSession _session;
        private IClientTransport _clientTransport;

        private BehaviorSubject<ImmutableArray<Type>> _firstRequestResponseMessageTypes =
            new BehaviorSubject<ImmutableArray<Type>>(ImmutableArray<Type>.Empty);

        private ImmutableDictionary<Type, MessageSendingFlags> _messageSendingFlags = ImmutableDictionary<Type, MessageSendingFlags>.Empty;
        private IMTProtoMessenger _messenger;
        private IRequestsManager _requestsManager = new RequestsManager();

        public MTProtoClientConnection([NotNull] IClientTransport clientTransport,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IMTProtoSession session,
            [NotNull] IMTProtoMessenger messenger)
        {
            if (clientTransport == null)
                throw new ArgumentNullException("clientTransport");
            if (messageIdGenerator == null)
                throw new ArgumentNullException("messageIdGenerator");
            if (session == null)
                throw new ArgumentNullException("session");
            if (messenger == null)
                throw new ArgumentNullException("messenger");

            _clientTransport = clientTransport;
            _messenger = messenger;
            _session = session;
            _messenger = messenger;

            DefaultResponseTimeout = MTProtoDefaults.ResponseTimeout;

            WireMessagesPipelines();

            _methods = new MTProtoAsyncMethods(this);
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
                return _clientTransport;
            }
        }

        public Task<TransportConnectResult> ConnectAsync()
        {
            return _clientTransport.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return _clientTransport.DisconnectAsync();
        }

        public bool IsConnected
        {
            get { return _clientTransport.IsConnected; }
        }

        public void SetAuthInfo(AuthInfo authInfo)
        {
            ThrowIfDisposed();
            _session.AuthInfo = authInfo;
        }

        public void SetSessionId(ulong sessionId)
        {
            ThrowIfDisposed();
            _session.SetSessionId(sessionId);
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

            Request<TResponse> request = CreateRequest<TResponse>(requestBody, flags, cancellationToken);

            Log.Debug(string.Format("Sending request ({0}) '{1}'.", flags, requestBody));

            request.Send();
            return await request.GetResponseAsync().ToObservable().Timeout(timeout).SingleAsync();
        }

        public Task<TResponse> RpcAsync<TResponse>(object requestBody)
        {
            return RequestAsync<TResponse>(requestBody, GetMessageSendingFlags(requestBody));
        }

        public Task SendAsync(object requestBody)
        {
            ThrowIfDisposed();
            _session.Send(requestBody, GetMessageSendingFlags(requestBody));
            return TaskConstants.Completed;
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

        private void WireMessagesPipelines()
        {
            // Ignore subscriptions disposables. On disposing all subscriptions will be disposed along with observables/producers.

            // Incoming messages pipeline.
            _clientTransport.Subscribe(messageBucket => _messenger.ProcessIncomingMessageBytesAsync(messageBucket));
            _messenger.IncomingMessages.Subscribe(_session);

            IMessageProducer inSessionMessages = _session.IncomingMessages;
            inSessionMessages.Subscribe(new MessageContainerHandler(_session));
            inSessionMessages.Subscribe(new BadMsgNotificationHandler(_session, _requestsManager));
            inSessionMessages.Subscribe(new RpcResultHandler(_requestsManager));
            inSessionMessages.Subscribe(new SessionHandler());
            inSessionMessages.Subscribe(new FirstRequestResponseHandler(_requestsManager, _firstRequestResponseMessageTypes));

            // Outgoing messages pipeline.
            _session.OutgoingMessages.Subscribe(messageEnvelope => _messenger.SendAsync(messageEnvelope));
            _messenger.OutgoingMessageBytesBuckets.Subscribe(bucket => _clientTransport.SendAsync(bucket));
        }

        private Request<TResponse> CreateRequest<TResponse>(object messageBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var request = new Request<TResponse>(messageBody, flags, o => _session.Send(o, flags), _requestsManager, cancellationToken);

            ImmutableArray<Type> types = _firstRequestResponseMessageTypes.Value;
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

        #region Disposing

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_messenger != null)
                {
                    _messenger.Dispose();
                    _messenger = null;
                }
                if (_clientTransport != null)
                {
                    _clientTransport.Dispose();
                    _clientTransport = null;
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

        #endregion
    }
}
