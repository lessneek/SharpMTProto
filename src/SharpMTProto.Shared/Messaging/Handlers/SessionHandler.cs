// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SessionHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System.Threading.Tasks;
    using Nito.AsyncEx;
    using Schema;

    public class SessionHandler : SingleMessageHandler<INewSession>
    {
        public override Task HandleAsync(IMessage message)
        {
            // TODO: implement.
            return TaskConstants.Completed;
        }
    }
}
