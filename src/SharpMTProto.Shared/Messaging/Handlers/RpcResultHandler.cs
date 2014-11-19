// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RpcResultHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Catel.Logging;
using Nito.AsyncEx;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    public class RpcResultHandler : ResponseHandler<IRpcResult>
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IRequestsManager _requestsManager;

        public RpcResultHandler(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        protected override Task HandleInternalAsync(IMessage responseMessage)
        {
            var rpcResult = (IRpcResult) responseMessage.Body;
            IRequest request = _requestsManager.Get(rpcResult.ReqMsgId);
            if (request == null)
            {
                Log.Warning(string.Format("Ignored response of type '{1}' for not existed request with MsgId: 0x{0:X8}.", rpcResult.ReqMsgId, rpcResult.Result.GetType()));
                return TaskConstants.Completed;
            }
            request.SetResponse(rpcResult.Result);
            return TaskConstants.Completed;
        }
    }
}
