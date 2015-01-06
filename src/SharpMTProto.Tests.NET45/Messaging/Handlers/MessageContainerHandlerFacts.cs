// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageContainerHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests.Messaging.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;
    using SharpMTProto.Messaging.Handlers;
    using SharpMTProto.Schema;

    [TestFixture]
    [Category("Messaging.Handlers")]
    public class MessageContainerHandlerFacts
    {
        [Test]
        public async Task Should_handle_container_and_forward_internal_messages_to_dispatcher()
        {
            var messages = new List<Message> {new Message(1, 1, 1), new Message(2, 2, 2), new Message(3, 3, 3)};
            var containerMessage = new MessageEnvelope(new Message(4, 4, new MsgContainer {Messages = messages}));
            List<Message> expectedMessages = messages.CloneTLObject();
            var receivedMessages = new List<IMessageEnvelope>();

            var messageHandlersHubMock = new Mock<IMessageHandler>();
            messageHandlersHubMock.Setup(dispatcher => dispatcher.Handle(It.IsAny<IMessageEnvelope>()))
                .Callback<IMessageEnvelope>(receivedMessages.Add);

            var handler = new MessageContainerHandler(messageHandlersHubMock.Object);
            await handler.HandleAsync(containerMessage);

            receivedMessages.Select(envelope => envelope.Message).Should().Equal(expectedMessages);
        }
    }
}
