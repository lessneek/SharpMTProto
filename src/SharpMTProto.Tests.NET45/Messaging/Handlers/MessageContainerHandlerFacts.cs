//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Messaging.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        public void Should_handle_container_and_forward_internal_messages_to_dispatcher()
        {
            var messages = new List<Message> {new Message(1, 1, 1), new Message(2, 2, 2), new Message(3, 3, 3)};
            MessageEnvelope containerMessage = MessageEnvelope.CreateEncrypted(new MTProtoSessionTag(123, 321),
                999,
                new Message(4, 4, new MsgContainer {Messages = messages}));

            List<Message> expectedMessages = messages.CloneTLObject();
            var receivedMessages = new List<IMessageEnvelope>();

            var messageHandlersHubMock = new Mock<IObserver<IMessageEnvelope>>();
            messageHandlersHubMock.Setup(dispatcher => dispatcher.OnNext(It.IsAny<IMessageEnvelope>()))
                .Callback<IMessageEnvelope>(receivedMessages.Add);

            var handler = new MessageContainerHandler();
            handler.Subscribe(messageHandlersHubMock.Object);
            handler.OnNext(containerMessage);

            receivedMessages.Select(envelope => envelope.Message).Should().Equal(expectedMessages);
        }
    }
}
