// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FirstRequestResponseHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;
    using Schema;
    using SharpMTProto.Messaging;
    using SharpMTProto.Messaging.Handlers;
    using TestObjects;

    [TestFixture]
    [Category("Messaging.Handlers")]
    public class FirstRequestResponseHandlerFacts
    {
        [Test]
        public async Task Should_set_response_to_request()
        {
            var request = new Mock<IRequest>();
            request.SetupGet(request1 => request1.MessageEnvelope).Returns(() => new MessageEnvelope(new Message(1, 1, new TestRequest {TestId = 1})));

            var response = new TestResponse {TestId = 1, TestText = "Simple test text."};
            var responseMessage = new MessageEnvelope(new Message(1, 1, response));

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.GetFirstOrDefaultWithUnsetResponse(response, It.IsAny<bool>()))
                .Returns(request.Object)
                .Verifiable();

            var messageTypes = new BehaviorSubject<ImmutableArray<Type>>(ImmutableArray.Create(typeof (TestResponse)));

            var handler = new FirstRequestResponseHandler(requestsManager.Object, messageTypes);
            handler.CanHandle(responseMessage).Should().BeTrue();
            await handler.HandleAsync(responseMessage);

            requestsManager.Verify();
            request.Verify(request1 => request1.SetResponse(response), Times.Once());
        }
    }
}
