//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.SessionModules
{
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.SessionModules;
    using SharpMTProto.Tests.TestObjects;

    [TestFixture]
    [Category("SessionModules")]
    public class FirstRequestResponseSessionModuleTests
    {
        [Test]
        public async Task Should_set_response_to_request()
        {
            var request = new Mock<IRequest>();
            request.SetupGet(request1 => request1.MsgId).Returns(() => 1);

            var response = new TestResponse {TestId = 1, TestText = "Simple test text."};
            var responseMessage = new MovingMessageEnvelope(null, MessageEnvelope.CreatePlain(new Message(1, 1, response)));

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.GetFirstOrDefaultWithUnsetResponse(response, It.IsAny<bool>()))
                .Returns(request.Object)
                .Verifiable();

            var sessionModule = new FirstRequestResponseSessionModule(requestsManager.Object);
            await sessionModule.ProcessIncomingMessageAsync(null, responseMessage).ConfigureAwait(false);

            requestsManager.Verify();
            request.Verify(request1 => request1.SetResponse(response), Times.Once());
        }
    }
}
