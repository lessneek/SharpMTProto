//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMember.Global

#endregion

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Threading.Tasks;
    using SharpMTProto.Annotations;
    using SharpMTProto.Schema;

    public class ActionMessageHandler : IMessageHandler
    {
        private readonly Action<IMessageEnvelope> _action;

        public ActionMessageHandler([NotNull] Action<IMessageEnvelope> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            _action = action;
        }

        public Task HandleAsync(IMessageEnvelope messageEnvelope)
        {
            return Task.Run(() => Handle(messageEnvelope));
        }

        public void Handle(IMessageEnvelope messageEnvelope)
        {
            _action(messageEnvelope);
        }
    }
}
