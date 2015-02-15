//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.SessionModules
{
    using System.Reactive.Disposables;
    using System.Threading.Tasks;
    using Nito.AsyncEx;
    using SharpMTProto.Messaging;
    using SharpMTProto.Utils;

    public interface ISessionModule : ICancelable
    {
        Task ProcessIncomingMessageAsync(IMTProtoSession session, MovingMessageEnvelope movingMessageEnvelope);
    }

    public abstract class SessionModule : Cancelable, ISessionModule
    {
        public Task ProcessIncomingMessageAsync(IMTProtoSession session, MovingMessageEnvelope movingMessageEnvelope)
        {
            return IsDisposed ? TaskConstants.Completed : ProcessIncomingMessageInternal(session, movingMessageEnvelope);
        }

        protected abstract Task ProcessIncomingMessageInternal(IMTProtoSession session, MovingMessageEnvelope movingMessageEnvelope);
    }
}
