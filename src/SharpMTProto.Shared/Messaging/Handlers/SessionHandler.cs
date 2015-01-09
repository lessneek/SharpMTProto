//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using SharpMTProto.Schema;

    public class SessionHandler : SingleMessageHandler<INewSession>
    {
        protected override void HandleInternal(IMessageEnvelope message)
        {
            var newSession = message.Message.Body as NewSessionCreated;
            if (newSession != null)
            {
                // TODO: implement.
            }
        }
    }
}
