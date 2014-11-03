// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BadMsgNotificationHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SharpMTProto.Messaging;
using SharpMTProto.Messaging.Handlers;
using SharpMTProto.Schema;

namespace SharpMTProto.Tests.Messaging.Handlers
{
    [TestFixture]
    public class BadMsgNotificationHandlerFacts
    {
        [Test]
        public async Task Should_update_salt_and_resend_message_on_bad_server_salt_notification()
        {
            const ulong newSalt = 9;

            var reqMsg = new Message(0x100500, 1, new Object());
            var request = new Mock<IRequest>();
            request.SetupGet(r => r.Message).Returns(reqMsg);
            var resMsg = new Message(
                0x200600,
                2,
                new BadServerSalt {BadMsgId = reqMsg.MsgId, BadMsgSeqno = reqMsg.Seqno, ErrorCode = (uint) ErrorCode.IncorrectServerSalt, NewServerSalt = newSalt});

            var connection = new Mock<IMTProtoConnection>();
            var requestsManager = new Mock<IRequestsManager>();
            requestsManager.Setup(manager => manager.Get(reqMsg.MsgId)).Returns(request.Object);

            var handler = new BadMsgNotificationHandler(connection.Object, requestsManager.Object);
            await handler.HandleAsync(resMsg);

            connection.Verify(c => c.UpdateSalt(It.IsAny<ulong>()), Times.Once);
            connection.Verify(c => c.UpdateSalt(newSalt), Times.Once);

            requestsManager.Verify(manager => manager.Get(It.IsAny<ulong>()), Times.Once);
            requestsManager.Verify(manager => manager.Get(reqMsg.MsgId), Times.Once);

            request.Verify(r => r.SendAsync(), Times.Once);
        }
    }
}
