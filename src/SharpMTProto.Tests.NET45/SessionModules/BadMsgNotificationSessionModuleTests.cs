//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.SessionModules
{
    using System;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using SharpMTProto.Messaging;
    using SharpMTProto.Schema;
    using SharpMTProto.SessionModules;

    [TestFixture]
    [Category("SessionModules")]
    public class BadMsgNotificationSessionModuleTests
    {
        [Test]
        public async Task Should_update_salt_and_resend_message_on_bad_server_salt_notification()
        {
            const ulong newSalt = 9;

            IMessage reqMsg = new Message(0x100500, 1, new Object());
            var request = new Mock<IRequest>();
            request.SetupGet(r => r.MsgId).Returns(reqMsg.MsgId);
            var resMsg = new MovingMessageEnvelope(null,
                MessageEnvelope.CreatePlain(new Message(0x200600,
                    2,
                    new BadServerSalt
                    {
                        BadMsgId = reqMsg.MsgId,
                        BadMsgSeqno = reqMsg.Seqno,
                        ErrorCode = (uint) ErrorCode.IncorrectServerSalt,
                        NewServerSalt = newSalt
                    })));

            var mockSession = new Mock<IMTProtoSession>();
            mockSession.Setup(session => session.TryGetSentMessage(reqMsg.MsgId, out reqMsg)).Returns(true);
            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.Get(reqMsg.MsgId)).Returns(request.Object);

            var handler = new BadMsgNotificationSessionModule(requestsManager.Object);
            await handler.ProcessIncomingMessageAsync(mockSession.Object, resMsg).ConfigureAwait(false);

            mockSession.Verify(c => c.UpdateSalt(It.IsAny<ulong>()), Times.Once);
            mockSession.Verify(c => c.UpdateSalt(newSalt), Times.Once);

            requestsManager.Verify(manager => manager.Get(It.IsAny<ulong>()), Times.Once);
            requestsManager.Verify(manager => manager.Get(reqMsg.MsgId), Times.Once);

            request.Verify(r => r.Send(), Times.Once);
        }
    }
}
