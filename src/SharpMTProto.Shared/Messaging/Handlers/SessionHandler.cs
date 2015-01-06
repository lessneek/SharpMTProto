// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SessionHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using Schema;

    public class SessionHandler : SingleMessageHandler<INewSession>
    {
        public override void Handle(IMessageEnvelope message)
        {
            // TODO: implement.
        }
    }
}
