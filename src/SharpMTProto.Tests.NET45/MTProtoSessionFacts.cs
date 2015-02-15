//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable ClassNeverInstantiated.Local

#endregion

namespace SharpMTProto.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using FluentAssertions;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using SharpMTProto.Annotations;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Tests.SetUp;

    [TestFixture]
    public class MTProtoSessionFacts : SharpMTProtoTestBase
    {
        private class TestMTProtoSession : MTProtoSession
        {
            public TestMTProtoSession([NotNull] IMessageIdGenerator messageIdGenerator,
                [NotNull] IRandomGenerator randomGenerator,
                [NotNull] IAuthKeysProvider authKeysProvider) : base(messageIdGenerator, randomGenerator, authKeysProvider)
            {
            }

            protected override Task<MovingMessageEnvelope> SendInternalAsync(IMessageEnvelope messageEnvelope,
                CancellationToken cancellationToken = new CancellationToken())
            {
                return Task.FromResult(new MovingMessageEnvelope(null, messageEnvelope));
            }
        }

        public override void SetUp()
        {
            base.SetUp();
            Override(builder => { builder.RegisterType<TestMTProtoSession>().As<MTProtoSession>(); });
        }

        [Test]
        public async Task Should_handle_incoming_message()
        {
            var incomingMessages = new ConcurrentQueue<MovingMessageEnvelope>();
            var expMessageEnvelope = Fixture.Create<MovingMessageEnvelope>();

            var session = Resolve<MTProtoSession>();
            session.IncomingMessages.Subscribe(incomingMessages.Enqueue);

            incomingMessages.Should().HaveCount(0);

            await session.ProcessIncomingMessageAsync(expMessageEnvelope).ConfigureAwait(false);

            incomingMessages.Should().HaveCount(1);

            MovingMessageEnvelope messageEnvelope;
            incomingMessages.TryDequeue(out messageEnvelope).Should().BeTrue();
            messageEnvelope.Should().Be(expMessageEnvelope);
        }

        [Test]
        public async Task Should_handle_outgoing_message()
        {
            var outgoingMessages = new ConcurrentQueue<MovingMessageEnvelope>();
            var expMessageBody = Fixture.Create<object>();

            var session = Resolve<MTProtoSession>();
            session.OutgoingMessages.Subscribe(outgoingMessages.Enqueue);

            outgoingMessages.Should().HaveCount(0);

            session.EnqueueToSend(expMessageBody, true, false);

            await Task.Delay(100);

            outgoingMessages.Should().HaveCount(1);

            MovingMessageEnvelope messageEnvelope;
            outgoingMessages.TryDequeue(out messageEnvelope).Should().BeTrue();
            messageEnvelope.MessageEnvelope.Message.Body.Should().BeSameAs(expMessageBody);
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
            await Task.Delay(1).ConfigureAwait(false);
            await session.ProcessIncomingMessageAsync(Fixture.Create<MovingMessageEnvelope>()).ConfigureAwait(false);
            await Task.Delay(1).ConfigureAwait(false);
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
        public async Task Should_handle_container()
        {
            var messages = new List<Message> { new Message(1, 1, 1), new Message(2, 2, 2), new Message(3, 3, 3) };
            MessageEnvelope containerMessage = MessageEnvelope.CreateEncrypted(new MTProtoSessionTag(123, 321),
                999,
                new Message(4, 4, new MsgContainer { Messages = messages }));

            List<Message> expectedMessages = messages.CloneTLObject();
            var receivedMessages = new List<MovingMessageEnvelope>();

            var session = Resolve<MTProtoSession>();
            session.IncomingMessages.Subscribe(receivedMessages.Add);
            await session.ProcessIncomingMessageAsync(new MovingMessageEnvelope(null, containerMessage)).ConfigureAwait(false);

            receivedMessages.Select(envelope => envelope.MessageEnvelope.Message).Should().Equal(expectedMessages);
        }
    }
}
