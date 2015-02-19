//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Sessions.Modules
{
    using System.Threading.Tasks;
    using SharpMTProto.Messaging;
    using SharpMTProto.Utils;

    public class FirstRequestResponseSessionModule : SessionModule
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IRequestsManager _requestsManager;

        public FirstRequestResponseSessionModule(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        protected override async Task ProcessIncomingMessageInternal(IMTProtoSession session, MovingMessageEnvelope movingMessageEnvelope)
        {
            object messageBody = movingMessageEnvelope.MessageEnvelope.Message.Body;
            IRequest request = _requestsManager.GetFirstOrDefaultWithUnsetResponse(messageBody);
            if (request == null)
            {
                Log.Warning(string.Format("Request for response of type '{0}' not found.", messageBody.GetType()));
                return;
            }

            request.SetResponse(messageBody);
        }
    }
}
