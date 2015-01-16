//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMember.Global

#endregion

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using SharpMTProto.Annotations;
    using SharpMTProto.Schema;

    public class ActionMessageHandler : IObserver<IMessageEnvelope>
    {
        private readonly Action<IMessageEnvelope> _action;

        public ActionMessageHandler([NotNull] Action<IMessageEnvelope> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            _action = action;
        }

        public void OnNext(IMessageEnvelope messageEnvelope)
        {
            _action(messageEnvelope);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
