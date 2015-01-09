//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    public class RpcResultHandler : SingleMessageHandler<IRpcResult>
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IRequestsManager _requestsManager;

        public RpcResultHandler(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        protected override void HandleInternal(IMessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.Message.Body == null)
            {
                Log.Warning("[RpcResultHandler] accepted message with null body.");
                return;
            }

            var rpcResult = messageEnvelope.Message.Body as IRpcResult;
            if (rpcResult == null)
            {
                Log.Warning(string.Format("[RpcResultHandler] accepted a message envelope with message body of type: {0}, but expected: {1}.",
                    messageEnvelope.Message.Body.GetType(),
                    typeof (IRpcResult)));
                return;
            }

            object result = rpcResult.Result;

            IRequest request = _requestsManager.Get(rpcResult.ReqMsgId);
            if (request == null)
            {
                Log.Warning(string.Format("[RpcResultHandler] Ignored message of type '{1}' for not existed request with MsgId: 0x{0:X8}.",
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
