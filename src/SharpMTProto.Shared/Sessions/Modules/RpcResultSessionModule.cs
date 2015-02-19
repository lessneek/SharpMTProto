//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Sessions.Modules
{
    using System.Threading.Tasks;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    public class RpcResultSessionModule : SessionModule
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IRequestsManager _requestsManager;

        public RpcResultSessionModule(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        protected override async Task ProcessIncomingMessageInternal(IMTProtoSession session, MovingMessageEnvelope movingMessageEnvelope)
        {
            object messageBody = movingMessageEnvelope.MessageEnvelope.Message.Body;
            if (messageBody == null)
            {
                Log.Warning("[RpcResultSessionModule] accepted message with null body.");
                return;
            }

            var rpcResult = messageBody as IRpcResult;
            if (rpcResult == null)
            {
                Log.Warning(string.Format("[RpcResultSessionModule] accepted a message envelope with message body of type: {0}, but expected: {1}.",
                    messageBody.GetType(),
                    typeof (IRpcResult)));
                return;
            }

            object result = rpcResult.Result;

            IRequest request = _requestsManager.Get(rpcResult.ReqMsgId);
            if (request == null)
            {
                Log.Warning(string.Format("[RpcResultSessionModule] Ignored message of type '{1}' for not existed request with MsgId: 0x{0:X8}.",
                    rpcResult.ReqMsgId,
                    result.GetType()));
                return;
            }

            var rpcError = result as IRpcError;
            if (rpcError != null)
            {
                request.SetException(new RpcErrorException(rpcError));
            }
            else
            {
                request.SetResponse(result);
            }
        }
    }
}
