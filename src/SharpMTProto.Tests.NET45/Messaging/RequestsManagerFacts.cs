// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RequestsManagerFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpMTProto.Messaging;
using SharpMTProto.Tests.TestObjects;

namespace SharpMTProto.Tests.Messaging
{
    using System;

    [TestFixture]
    [Category("Messaging")]
    public class RequestsManagerFacts
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Should_get_first_or_default(bool includeRpc)
        {
            var response = new TestResponse {TestId = 1, TestText = "Test text."};

            var exReqMock = new Mock<IRequest>();
            exReqMock.Setup(r => r.CanSetResponse(It.IsAny<Type>())).Returns((Type type) => typeof(TestResponse).IsAssignableFrom(type));
            exReqMock.Setup(r => r.MessageEnvelope.Message.MsgId).Returns(0x100500);
            exReqMock.Setup(r => r.Flags).Returns(MessageSendingFlags.EncryptedAndContentRelatedRPC);
            var exReq = exReqMock.Object;

            var requestsManager = new RequestsManager();
            requestsManager.Add(exReq);

            IRequest request = requestsManager.GetFirstOrDefaultWithUnsetResponse(response, includeRpc);

            if (includeRpc)
            {
                request.Should().NotBeNull();
                request.Should().BeSameAs(exReq);
            }
            else
            {
                request.Should().BeNull();
            }
        }
    }
}
