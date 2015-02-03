//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Reactive.Subjects;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;
    using SharpMTProto.Messaging;
    using SharpMTProto.Messaging.Handlers;
    using SharpMTProto.Schema;
    using SharpMTProto.Tests.TestObjects;

    [TestFixture]
    [Category("Messaging.Handlers")]
    public class FirstRequestResponseHandlerFacts
    {
        [Test]
        public void Should_set_response_to_request()
        {
            var request = new Mock<IRequest>();
            request.SetupGet(request1 => request1.MsgId).Returns(() => 1);

            var response = new TestResponse {TestId = 1, TestText = "Simple test text."};
            var responseMessage = MessageEnvelope.CreatePlain(new Message(1, 1, response));

            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.GetFirstOrDefaultWithUnsetResponse(response, It.IsAny<bool>()))
                .Returns(request.Object)
                .Verifiable();

            var messageTypes = new BehaviorSubject<ImmutableArray<Type>>(ImmutableArray.Create(typeof (TestResponse)));

            var handler = new FirstRequestResponseHandler(requestsManager.Object, messageTypes);
            handler.CanHandle(responseMessage).Should().BeTrue();
            handler.OnNext(responseMessage);

            requestsManager.Verify();
            request.Verify(request1 => request1.SetResponse(response), Times.Once());
        }
    }
}
