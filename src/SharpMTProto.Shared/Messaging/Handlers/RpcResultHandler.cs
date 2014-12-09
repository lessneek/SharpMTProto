// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RpcResultHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;
    using Catel.Logging;
    using Schema;

    public class RpcResultHandler : SingleMessageHandler<IRpcResult>
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IRequestsManager _requestsManager;

        public RpcResultHandler(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        public override Task HandleAsync(IMessage message)
        {
            return Observable.Start(() =>
            {
                var rpcResult = (IRpcResult) message.Body;
                object result = rpcResult.Result;

                IRequest request = _requestsManager.Get(rpcResult.ReqMsgId);
                if (request == null)
                {
                    Log.Warning(string.Format("Ignored message of type '{1}' for not existed request with MsgId: 0x{0:X8}.",
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
            }).ToTask();
        }
    }
}
