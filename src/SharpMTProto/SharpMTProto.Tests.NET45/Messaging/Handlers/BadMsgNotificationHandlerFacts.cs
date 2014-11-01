// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BadMsgNotificationHandlerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using NSubstitute;
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
        public void Should_update_salt_and_resend_message_on_bad_server_salt_notification()
        {
            const ulong newSalt = 9;

            var reqMsg = new Message(0x100500, 1, new Object());
            var req = Substitute.For<IRequest>();
            req.Message.Returns(reqMsg);
            var resMsg = new Message(
                0x200600,
                2,
                new BadServerSalt {BadMsgId = reqMsg.MsgId, BadMsgSeqno = reqMsg.Seqno, ErrorCode = (uint) ErrorCode.IncorrectServerSalt, NewServerSalt = newSalt});

            var connection = Substitute.For<IMTProtoConnection>();
            var requestsManager = Substitute.For<IRequestsManager>();
            requestsManager.Get(reqMsg.MsgId).Returns(req);

            var handler = new BadMsgNotificationHandler(connection, requestsManager);

            handler.HandleAsync(resMsg);

            connection.Received(1).UpdateSalt(Arg.Any<ulong>());
            connection.Received(1).UpdateSalt(newSalt);

            requestsManager.Received(1).Get(Arg.Any<ulong>());
            requestsManager.Received(1).Get(reqMsg.MsgId);
        }
    }
}
