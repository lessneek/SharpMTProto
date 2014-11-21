// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RpcResultHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SharpMTProto.Messaging;
using SharpMTProto.Messaging.Handlers;
using SharpMTProto.Schema;
using SharpMTProto.Tests.TestObjects;

namespace SharpMTProto.Tests.Messaging.Handlers
{
    [TestFixture]
    [Category("Messaging.Handlers")]
    public class RpcResultHandlerFacts
    {
        [Test]
        public async Task Should_set_exception_on_rpc_error_response()
        {
            var reqMsg = new Message(0x100500, 1, new TestRequest {TestId = 1});
            
            var rpcResult = new RpcResult
            {
                ReqMsgId = reqMsg.MsgId,
                Result = new RpcError {ErrorCode = 400, ErrorMessage = "BAD_REQUEST"}
            };
            var resMsg = new Message(0x200600, 2, rpcResult);

            var request = new Mock<IRequest>();
            request.SetupGet(r => r.Message).Returns(reqMsg);

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.Get(reqMsg.MsgId)).Returns(request.Object);

            var handler = new RpcResultHandler(requestsManager.Object);
            await handler.HandleAsync(resMsg);

            requestsManager.Verify(manager => manager.Get(It.IsAny<ulong>()), Times.Once);
            requestsManager.Verify(manager => manager.Get(reqMsg.MsgId), Times.Once);

            request.Verify(r => r.SetException(It.IsAny<Exception>()), Times.Once);
            request.Verify(
                r => r.SetException(It.Is<RpcErrorException>(exception => exception.Error == rpcResult.Result)),
                Times.Once);
        }

        [Test]
        public async Task Should_set_rpc_result_to_requst()
        {
            var reqMsg = new Message(0x100500, 1, new TestRequest {TestId = 1});
            
            var rpcResult = new RpcResult
            {
                ReqMsgId = reqMsg.MsgId,
                Result = new TestResponse {TestId = 1, TestText = "THIS IS RESPONSE!"}
            };
            var resMsg = new Message(0x200600, 2, rpcResult);

            var request = new Mock<IRequest>();
            request.SetupGet(r => r.Message).Returns(reqMsg);

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.Get(reqMsg.MsgId)).Returns(request.Object);

            var handler = new RpcResultHandler(requestsManager.Object);
            await handler.HandleAsync(resMsg);

            requestsManager.Verify(manager => manager.Get(It.IsAny<ulong>()), Times.Once);
            requestsManager.Verify(manager => manager.Get(reqMsg.MsgId), Times.Once);

            request.Verify(r => r.SetResponse(It.IsAny<object>()), Times.Once);
            request.Verify(r => r.SetResponse(rpcResult.Result), Times.Once);
        }
    }
}
