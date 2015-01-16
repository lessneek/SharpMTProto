//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using FluentAssertions;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using SharpMTProto.Messaging;
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
        public void Should_handle_outgoing_message()
        {
            var outgoingMessages = new ConcurrentQueue<IMessageEnvelope>();
            var expMessageBody = Fixture.Create<object>();

            var session = Resolve<MTProtoSession>();
            session.OutgoingMessages.Subscribe(outgoingMessages.Enqueue);

            outgoingMessages.Should().HaveCount(0);

            session.Send(expMessageBody, MessageSendingFlags.EncryptedAndContentRelatedRPC);

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
            session.Send(Fixture.Create<object>(), MessageSendingFlags.EncryptedAndContentRelatedRPC);
            await Task.Delay(1);
            session.LastActivity.Should().BeAfter(initialActivity).And.BeBefore(DateTime.UtcNow);
        }
    }
}
