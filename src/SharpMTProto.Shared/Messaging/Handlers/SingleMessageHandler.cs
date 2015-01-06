// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SingleMessageHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System.Collections.Immutable;
    using Schema;

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
