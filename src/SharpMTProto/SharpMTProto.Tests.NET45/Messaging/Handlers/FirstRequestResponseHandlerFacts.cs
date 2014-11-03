// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FirstRequestResponseHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

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
    public class FirstRequestResponseHandlerFacts
    {
        [Test]
        public async Task Should_set_response_to_request()
        {
            var request = new Mock<IRequest>();
            request.SetupGet(request1 => request1.Message).Returns(() => new Message(1, 1, new TestRequest {TestId = 1}));

            var response = new TestResponse {TestId = 1, TestText = "Simple test text."};
            var responseMessage = new Message(1, 1, response);

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.GetFirstOrDefault(response)).Returns(request.Object).Verifiable();

            var handler = new FirstRequestResponseHandler(requestsManager.Object);
            await handler.HandleAsync(responseMessage);

            requestsManager.Verify();
            request.Verify(request1 => request1.SetResponse(response), Times.Once());
        }
    }
}
