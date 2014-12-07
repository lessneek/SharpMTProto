// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageHandlersHub.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;
    using Catel.Logging;
    using Schema;

    public class MessageHandlersHub : MessageHandler
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private Subject<IMessage> _messages = new Subject<IMessage>();
        private ImmutableArray<IMessageHandler> _messageHandlers = ImmutableArray<IMessageHandler>.Empty;
        private readonly object _messageTypesSyncRoot = new object();

        public MessageHandlersHub(params IMessageHandler[] messageHandlers)
        {
            Add(messageHandlers);
        }

        public void Add(params IMessageHandler[] messageHandlers)
        {
            if (messageHandlers.Length == 0)
            {
                return;
            }

            ImmutableInterlocked.InterlockedExchange(ref _messageHandlers, _messageHandlers.AddRange(messageHandlers));

            foreach (IMessageHandler handler in _messageHandlers)
            {
                handler.SubscribeTo(_messages);
                handler.MessageTypesUpdates.Subscribe(OnMessageTypesUpdate);
            }

            UpdateMessageTypes();
        }

        private void OnMessageTypesUpdate(MessageTypesUpdate messageTypesUpdate)
        {
            lock (_messageTypesSyncRoot)
            {
                var builder = MessageTypes.ToBuilder();
                foreach (var addedType in messageTypesUpdate.AddedTypes)
                {
                    if (!builder.Contains(addedType))
                    {
                        builder.Add(addedType);
                    }
                }
                foreach (var removedType in messageTypesUpdate.RemovedTypes)
                {
                    if (builder.Contains(removedType))
                    {
                        builder.Remove(removedType);
                    }
                }
                MessageTypes = builder.ToImmutable();
            }
        }

        private void UpdateMessageTypes()
        {
            var builder = ImmutableArray.CreateBuilder<Type>();
            foreach (IMessageHandler handler in _messageHandlers)
            {
                builder.AddRange(handler.MessageTypes);
            }
            MessageTypes = builder.ToImmutable();
        }

        public override Task HandleAsync(IMessage message)
        {
            ThrowIfDisposed();
            return Observable.Start(() => _messages.OnNext(message)).ToTask();
        }

        public override bool CanHandle(IMessage message)
        {
            var canHandle = base.CanHandle(message);
            if (!canHandle)
            {
                Log.Warning(string.Format("Message handlers hub couldn't handle a message of type {0}.", message.Body.GetType()));
            }
            return canHandle;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_messages != null)
                {
                    _messages.Dispose();
                    _messages = null;
                }
                if (_messageHandlers.Length > 0)
                {
                    foreach (IMessageHandler handler in _messageHandlers)
                    {
                        handler.Dispose();
                    }
                    _messageHandlers = _messageHandlers.Clear();
                }
            }
            base.Dispose(disposing);
        }
    }
}
