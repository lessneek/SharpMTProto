//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    public abstract class MessageHandler : Cancelable, IObserver<IMessageEnvelope>
    {
        private ImmutableArray<Type> _messageTypes = ImmutableArray<Type>.Empty;

        public ImmutableArray<Type> MessageTypes
        {
            get { return _messageTypes; }
            protected set { ImmutableInterlocked.InterlockedExchange(ref _messageTypes, value); }
        }

        public void OnNext(IMessageEnvelope messageEnvelope)
        {
            if (IsDisposed)
                return;

            if (!CanHandle(messageEnvelope))
                return;

            HandleInternal(messageEnvelope);
        }

        public virtual void OnError(Exception error)
        {
        }

        public virtual void OnCompleted()
        {
        }

        protected abstract void HandleInternal(IMessageEnvelope messageEnvelope);

        public virtual bool CanHandle(IMessageEnvelope messageEnvelope)
        {
            return MessageTypes.Any(type => type.GetTypeInfo().IsAssignableFrom(messageEnvelope.Message.Body.GetType().GetTypeInfo()));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _messageTypes = ImmutableArray<Type>.Empty;
            }
            base.Dispose(disposing);
        }
    }
}
