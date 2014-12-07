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
    using System.Reactive.Concurrency;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;
    using Catel.Reflection;
    using Schema;
    using Utils;

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
        /// <param name="message">A message.</param>
        bool CanHandle(IMessage message);

        /// <summary>
        ///     Subscribes handler for a message stream.
        /// </summary>
        /// <param name="observable">A messages stream.</param>
        void SubscribeTo(IObservable<IMessage> observable);

        /// <summary>
        ///     Unsubscribe from current messages stream.
        /// </summary>
        void Unsubscribe();

        /// <summary>
        ///     Handles a message asynchronously.
        /// </summary>
        /// <param name="message">A message to handle.</param>
        Task HandleAsync(IMessage message);
    }

    public abstract class MessageHandler : Cancelable, IMessageHandler, IObserver<IMessage>
    {
        private IDisposable _subscription;
        private ImmutableArray<Type> _messageTypes = ImmutableArray<Type>.Empty;
        private Subject<MessageTypesUpdate> _messageTypesUpdates = new Subject<MessageTypesUpdate>();

        protected virtual IScheduler ObserverScheduler
        {
            get { return Scheduler.Default; }
        }

        void IObserver<IMessage>.OnNext(IMessage message)
        {
            Task.Run(() => HandleAsync(message));
        }

        void IObserver<IMessage>.OnError(Exception error)
        {
            Unsubscribe();
        }

        void IObserver<IMessage>.OnCompleted()
        {
            Unsubscribe();
        }

        public abstract Task HandleAsync(IMessage message);

        public virtual ImmutableArray<Type> MessageTypes
        {
            get { return _messageTypes; }
            protected set
            {
                var oldTypes = ImmutableInterlocked.InterlockedExchange(ref _messageTypes, value);

                if (oldTypes.Length == 0 && value.Length == 0)
                {
                    return;
                }

                var addedTypes = (from type in value where !oldTypes.Contains(type) select type).ToImmutableArray();
                var removedTypes = (from type in oldTypes where !value.Contains(type) select type).ToImmutableArray();

                _messageTypesUpdates.OnNext(new MessageTypesUpdate(this, addedTypes, removedTypes));
            }
        }

        public IObservable<MessageTypesUpdate> MessageTypesUpdates
        {
            get { return _messageTypesUpdates; }
        }

        public virtual bool CanHandle(IMessage message)
        {
#if PCL
            return MessageTypes.Any(type => type.GetTypeInfo().IsInstanceOfType(message.Body));
#else
            return MessageTypes.Any(type => type.IsInstanceOfType(message.Body));
#endif
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

        public void SubscribeTo(IObservable<IMessage> observable)
        {
            Unsubscribe();
            _subscription = observable.Where(CanHandle).ObserveOn(ObserverScheduler).Subscribe(this);
        }

        public void Unsubscribe()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
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
