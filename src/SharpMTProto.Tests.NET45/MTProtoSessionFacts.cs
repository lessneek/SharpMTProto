//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using SharpMTProto.Schema;
    using SharpMTProto.Tests.SetUp;

    [TestFixture]
    public class MTProtoSessionFacts : SharpMTProtoTestBase
    {
        [Test]
        public void Should_handle_incoming_message()
        {
            var incomingMessages = new ConcurrentQueue<IMessageEnvelope>();
            var expMessageEnvelope = Fixture.Create<IMessageEnvelope>();

            var session = Resolve<MTProtoSession>();
            session.IncomingMessages.Subscribe(incomingMessages.Enqueue);

            incomingMessages.Should().HaveCount(0);

            session.OnNext(expMessageEnvelope);

            incomingMessages.Should().HaveCount(1);

            IMessageEnvelope messageEnvelope;
            incomingMessages.TryDequeue(out messageEnvelope).Should().BeTrue();
            messageEnvelope.Should().BeSameAs(expMessageEnvelope);
        }

        [Test]
        public async Task Should_handle_outgoing_message()
        {
            var outgoingMessages = new ConcurrentQueue<IMessageEnvelope>();
            var expMessageBody = Fixture.Create<object>();

            var session = Resolve<MTProtoSession>();
            session.OutgoingMessages.Subscribe(outgoingMessages.Enqueue);

            outgoingMessages.Should().HaveCount(0);

            session.EnqueueToSend(expMessageBody, true, false);

            await Task.Delay(100);

            outgoingMessages.Should().HaveCount(1);

            IMessageEnvelope messageEnvelope;
            outgoingMessages.TryDequeue(out messageEnvelope).Should().BeTrue();
            messageEnvelope.Message.Body.Should().BeSameAs(expMessageBody);
        }

        [Test]
        public async Task Initial_last_activity_should_be_before_now()
        {
            var session = Resolve<MTProtoSession>();

            DateTime initialActivity = session.LastActivity;
            await Task.Delay(1);
            initialActivity.Should().BeBefore(DateTime.UtcNow);
        }

        [Test]
        public async Task Handling_of_incoming_message_should_update_last_activity()
        {
            var session = Resolve<MTProtoSession>();

            DateTime initialActivity = session.LastActivity;
            await Task.Delay(1);
            session.OnNext(Fixture.Create<IMessageEnvelope>());
            await Task.Delay(1);
            session.LastActivity.Should().BeAfter(initialActivity).And.BeBefore(DateTime.UtcNow);
        }

        [Test]
        public async Task Sending_of_outgoing_message_should_update_last_activity()
        {
            var session = Resolve<MTProtoSession>();

            DateTime initialActivity = session.LastActivity;
            await Task.Delay(1);
            session.EnqueueToSend(Fixture.Create<object>(), true, false);
            await Task.Delay(1);
            session.LastActivity.Should().BeAfter(initialActivity).And.BeBefore(DateTime.UtcNow);
        }

        [Test]
        public void Should_handle_container()
        {
            var messages = new List<Message> { new Message(1, 1, 1), new Message(2, 2, 2), new Message(3, 3, 3) };
            MessageEnvelope containerMessage = MessageEnvelope.CreateEncrypted(new MTProtoSessionTag(123, 321),
                999,
                new Message(4, 4, new MsgContainer { Messages = messages }));

            List<Message> expectedMessages = messages.CloneTLObject();
            var receivedMessages = new List<IMessageEnvelope>();

            var session = Resolve<MTProtoSession>();
            session.IncomingMessages.Subscribe(receivedMessages.Add);
            session.OnNext(containerMessage);

            receivedMessages.Select(envelope => envelope.Message).Should().Equal(expectedMessages);
        }
    }
}
