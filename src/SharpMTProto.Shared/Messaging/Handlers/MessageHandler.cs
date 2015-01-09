//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    public abstract class MessageHandler : Cancelable, IMessageHandler
    {
        private ImmutableArray<Type> _messageTypes = ImmutableArray<Type>.Empty;

        public ImmutableArray<Type> MessageTypes
        {
            get { return _messageTypes; }
            protected set { ImmutableInterlocked.InterlockedExchange(ref _messageTypes, value); }
        }

        public Task HandleAsync(IMessageEnvelope messageEnvelope)
        {
            return Task.Run(() => Handle(messageEnvelope));
        }

        public void Handle(IMessageEnvelope messageEnvelope)
        {
            if (IsDisposed)
                return;

            if (!CanHandle(messageEnvelope))
                return;

            HandleInternal(messageEnvelope);
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
