//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using System.Collections.Immutable;
    using SharpMTProto.Schema;

    public abstract class SingleMessageHandler<TMessage> : MessageHandler where TMessage : class
    {
        protected SingleMessageHandler()
        {
            MessageTypes = ImmutableArray.Create(typeof (TMessage));
        }

        public override bool CanHandle(IMessageEnvelope messageEnvelope)
        {
            return messageEnvelope.Message.Body is TMessage;
        }
    }
}
