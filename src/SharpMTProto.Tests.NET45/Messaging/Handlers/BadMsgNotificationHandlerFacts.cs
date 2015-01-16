//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Messaging.Handlers
{
    using System;
    using Moq;
    using NUnit.Framework;
    using SharpMTProto.Messaging;
    using SharpMTProto.Messaging.Handlers;
    using SharpMTProto.Schema;

    [TestFixture]
    [Category("Messaging.Handlers")]
    public class BadMsgNotificationHandlerFacts
    {
        [Test]
        public void Should_update_salt_and_resend_message_on_bad_server_salt_notification()
        {
            const ulong newSalt = 9;

            var reqMsg = new Message(0x100500, 1, new Object());
            var reqMsgEvelope = new MessageEnvelope(reqMsg);
            var request = new Mock<IRequest>();
            request.SetupGet(r => r.MessageEnvelope).Returns(reqMsgEvelope);
            var resMsg =
                new MessageEnvelope(new Message(0x200600,
                    2,
                    new BadServerSalt
                    {
                        BadMsgId = reqMsg.MsgId,
                        BadMsgSeqno = reqMsg.Seqno,
                        ErrorCode = (uint) ErrorCode.IncorrectServerSalt,
                        NewServerSalt = newSalt
                    }));

            var mockSession = new Mock<IMTProtoSession>();
            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.Get(reqMsg.MsgId)).Returns(request.Object);

            var handler = new BadMsgNotificationHandler(mockSession.Object, requestsManager.Object);
            handler.OnNext(resMsg);

            mockSession.Verify(c => c.UpdateSalt(It.IsAny<ulong>()), Times.Once);
            mockSession.Verify(c => c.UpdateSalt(newSalt), Times.Once);

            requestsManager.Verify(manager => manager.Get(It.IsAny<ulong>()), Times.Once);
            requestsManager.Verify(manager => manager.Get(reqMsg.MsgId), Times.Once);

            request.Verify(r => r.Send(), Times.Once);
        }
    }
}
