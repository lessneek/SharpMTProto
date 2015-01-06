// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reflection;
    using System.Threading.Tasks;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    /// <summary>
    ///     Message handler.
    /// </summary>
    public interface IMessageHandler : ICancelable
    {
        /// <summary>
        ///     Supported message types.
        /// </summary>
        ImmutableArray<Type> MessageTypes { get; }

        IObservable<MessageTypesUpdate> MessageTypesUpdates { get; }

        /// <summary>
        ///     Determines can handle a message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope.</param>
        bool CanHandle(IMessageEnvelope messageEnvelope);

        /// <summary>
        ///     Subscribes handler for a message stream.
        /// </summary>
        /// <param name="observable">A messages stream.</param>
        void SubscribeTo(IObservable<IMessageEnvelope> observable);

        /// <summary>
        ///     Unsubscribe from current messages stream.
        /// </summary>
        void Unsubscribe();

        /// <summary>
        ///     Handles a message asynchronously.
        /// </summary>
        /// <param name="messageEnvelope">A messageEnvelope to handle.</param>
        Task HandleAsync(IMessageEnvelope messageEnvelope);

        /// <summary>
        ///     Handles a message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope to handle.</param>
        void Handle(IMessageEnvelope messageEnvelope);
    }

    public abstract class MessageHandler : Cancelable, IMessageHandler, IObserver<IMessageEnvelope>
    {
        private ImmutableArray<Type> _messageTypes = ImmutableArray<Type>.Empty;
        private Subject<MessageTypesUpdate> _messageTypesUpdates = new Subject<MessageTypesUpdate>();
        private IDisposable _subscription;

        public Task HandleAsync(IMessageEnvelope messageEnvelope)
        {
            return Task.Run(() => Handle(messageEnvelope));
        }

        public abstract void Handle(IMessageEnvelope messageEnvelope);

        public ImmutableArray<Type> MessageTypes
        {
            get { return _messageTypes; }
            protected set
            {
                ImmutableArray<Type> oldTypes = ImmutableInterlocked.InterlockedExchange(ref _messageTypes, value);

                if (oldTypes.Length == 0 && value.Length == 0)
                {
                    return;
                }

                ImmutableArray<Type> addedTypes = (from type in value where !oldTypes.Contains(type) select type).ToImmutableArray();
                ImmutableArray<Type> removedTypes = (from type in oldTypes where !value.Contains(type) select type).ToImmutableArray();

                _messageTypesUpdates.OnNext(new MessageTypesUpdate(this, addedTypes, removedTypes));
            }
        }

        public IObservable<MessageTypesUpdate> MessageTypesUpdates
        {
            get { return _messageTypesUpdates; }
        }

        public virtual bool CanHandle(IMessageEnvelope messageEnvelope)
        {
            return MessageTypes.Any(type => type.GetTypeInfo().IsAssignableFrom(messageEnvelope.Message.Body.GetType().GetTypeInfo()));
        }

        public void SubscribeTo(IObservable<IMessageEnvelope> observable)
        {
            Unsubscribe();
            _subscription = observable.Where(CanHandle).Subscribe(this);
        }

        public void Unsubscribe()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
        }

        void IObserver<IMessageEnvelope>.OnNext(IMessageEnvelope message)
        {
            HandleAsync(message);
        }

        void IObserver<IMessageEnvelope>.OnError(Exception error)
        {
            Unsubscribe();
        }

        void IObserver<IMessageEnvelope>.OnCompleted()
        {
            Unsubscribe();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Unsubscribe();
                if (_messageTypesUpdates != null)
                {
                    _messageTypesUpdates.OnCompleted();
                    _messageTypesUpdates.Dispose();
                    _messageTypesUpdates = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    public class MessageTypesUpdate
    {
        public MessageTypesUpdate(IMessageHandler sender, ImmutableArray<Type> addedTypes, ImmutableArray<Type> removedTypes)
        {
            Sender = sender;
            AddedTypes = addedTypes;
            RemovedTypes = removedTypes;
        }

        public IMessageHandler Sender { get; private set; }
        public ImmutableArray<Type> AddedTypes { get; private set; }
        public ImmutableArray<Type> RemovedTypes { get; private set; }
    }
}
