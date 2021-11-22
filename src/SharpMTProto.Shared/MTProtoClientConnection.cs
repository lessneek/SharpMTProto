﻿//////////////////////////////////////////////////////////
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
    using System.Diagnostics;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reactive.Threading.Tasks;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using BigMath.Utils;
    using Nito.AsyncEx;
    using SharpMTProto.Annotations;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Sessions;
    using SharpMTProto.Sessions.Modules;
    using SharpMTProto.Transport;

    /// <summary>
    ///     Interface of a client MTProto connection.
    /// </summary>
    public interface IMTProtoClientConnection : IMTProtoSession, IRemoteProcedureCaller
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
        Task<TResponse> RequestAsync<TResponse>(object requestBody, MessageSendingFlags flags, TimeSpan timeout, CancellationToken cancellationToken);

        Task<TransportConnectResult> ConnectAsync();
        Task DisconnectAsync();
    }

    /// <summary>
    ///     Client MTProto connection.
    /// </summary>
    public class MTProtoClientConnection : MTProtoSession, IMTProtoClientConnection
    {
        private readonly IMessageCodec _messageCodec;
        private readonly object _messageSendingFlagsSyncRoot = new object();
        private readonly MTProtoAsyncMethods _methods;
        private IConnectableClientTransport _clientTransport;

        private BehaviorSubject<ImmutableArray<Type>> _firstRequestResponseMessageTypes =
            new BehaviorSubject<ImmutableArray<Type>>(ImmutableArray<Type>.Empty);

        private ImmutableDictionary<Type, MessageSendingFlags> _messageSendingFlags = ImmutableDictionary<Type, MessageSendingFlags>.Empty;
        private IRequestsManager _requestsManager;

        public MTProtoClientConnection([NotNull] IConnectableClientTransport clientTransport,
            [NotNull] IMessageCodec messageCodec,
            [NotNull] IMessageIdGenerator messageIdGenerator,
            [NotNull] IRandomGenerator randomGenerator,
            [NotNull] IAuthKeysProvider authKeysProvider,
            [NotNull] IRequestsManager requestsManager) : base(messageIdGenerator, randomGenerator, authKeysProvider)
        {
            if (clientTransport == null)
                throw new ArgumentNullException("clientTransport");
            if (messageCodec == null)
                throw new ArgumentNullException("messageCodec");
            if (requestsManager == null)
                throw new ArgumentNullException("requestsManager");

            _clientTransport = clientTransport;
            _messageCodec = messageCodec;
            _requestsManager = requestsManager;

            DefaultResponseTimeout = MTProtoDefaults.ResponseTimeout;

            WireMessagesPipelines();

            _methods = new MTProtoAsyncMethods(this);
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
            MessageSendingFlags flags = GetMessageSendingFlags(requestBody);
            EnqueueToSend(requestBody, flags.HasFlag(MessageSendingFlags.ContentRelated), flags.HasFlag(MessageSendingFlags.Encrypted));
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
            _messageCodec.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        private void WireMessagesPipelines()
        {
            // Ignore subscriptions disposables. On disposing all subscriptions will be disposed along with observables/producers.

            // Incoming messages pipeline.
            _clientTransport.Subscribe(async messageBytesBucket =>
            {
                LogMessageInOut(messageBytesBucket, "IN");
                IMessageEnvelope messageEnvelope = await _messageCodec.DecodeMessageAsync(messageBytesBucket, MessageCodecMode.Server);
                await ProcessIncomingMessageAsync(new MovingMessageEnvelope(_clientTransport, messageEnvelope)).ConfigureAwait(false);
            });

            AddModule(new BadMsgNotificationSessionModule(_requestsManager));
            AddModule(new RpcResultSessionModule(_requestsManager));
            AddModule(new FirstRequestResponseSessionModule(_requestsManager));
        }

        protected override async Task<MovingMessageEnvelope> SendInternalAsync(IMessageEnvelope messageEnvelope, IClientTransport clientTransport = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            IBytesBucket messageBytesBucket = await _messageCodec.EncodeMessageAsync(messageEnvelope, MessageCodecMode.Client);
            LogMessageInOut(messageBytesBucket, "OUT");

            await _clientTransport.SendAsync(messageBytesBucket, cancellationToken).ConfigureAwait(false);

            return new MovingMessageEnvelope(_clientTransport, messageEnvelope);
        }

        private Request<TResponse> CreateRequest<TResponse>(object messageBody, MessageSendingFlags flags, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var request = new Request<TResponse>(messageBody,
                flags,
                o => EnqueueToSend(o, flags.HasFlag(MessageSendingFlags.ContentRelated), flags.HasFlag(MessageSendingFlags.Encrypted)),
                _requestsManager,
                cancellationToken);

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

        #region Logging

        [Conditional("DEBUG")]
        private static void LogMessageInOut(IBytesBucket messageBytes, string inOrOut)
        {
            ArraySegment<byte> bytes = messageBytes.UsedBytes;
            Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, bytes.Count, bytes.ToHexString()));
        }

        [Conditional("DEBUG")]
        private static void Debug(string message)
        {
            Log.Debug(string.Format("[MTProtoClientConnection] : {0}", message));
        }

        #endregion
    }
}
