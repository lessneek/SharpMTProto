// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ResponseDispatcherFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SharpMTProto.Messaging.Handlers;
using SharpMTProto.Schema;
using SharpMTProto.Tests.TestObjects;

namespace SharpMTProto.Tests.Messaging.Handlers
{
    [TestFixture]
    public class ResponseDispatcherFacts
    {
        [Test]
        public async Task Should_dispatch_messages()
        {
            var msg1 = new Message(1, 2, new TestResponse {TestId = 3, TestText = "TEXT 1"});
            var msg1Ex = new Message(3, 4, new TestResponseEx {TestId = 3, TestText = "TEXT 1", TestText2 = "TEXT 1 EX"});
            var msg2 = new Message(5, 6, new TestResponse2 {Name = "Mr.Resp", Address = "1 Object st.", City = "Class"});
            var msg3 = new Message(7, 8, 9);

            Expression<Func<IResponseHandler, Task>> handleAnyMsgExp = handler => handler.HandleAsync(It.IsAny<IMessage>());

            var handler1 = new Mock<IResponseHandler>();
            handler1.SetupGet(handler => handler.ResponseType).Returns(typeof (ITestResponse));

            var handler2 = new Mock<IResponseHandler>();
            handler2.SetupGet(handler => handler.ResponseType).Returns(typeof (ITestResponse2));

            var handlerF = new Mock<IResponseHandler>();
            handlerF.SetupGet(handler => handler.ResponseType).Returns(typeof (object));

            var dispatcher = new ResponseDispatcher();
            dispatcher.AddHandler(handler1.Object);
            dispatcher.AddHandler(handler2.Object);
            dispatcher.FallbackHandler = handlerF.Object;

            await dispatcher.DispatchAsync(msg1);

            handler1.Verify(handler => handler.HandleAsync(msg1));
            handler1.Verify(handleAnyMsgExp, Times.Once());
            handler2.Verify(handleAnyMsgExp, Times.Never());
            handlerF.Verify(handleAnyMsgExp, Times.Never());

            await dispatcher.DispatchAsync(msg1Ex);

            handler1.Verify(handler => handler.HandleAsync(msg1Ex));
            handler1.Verify(handleAnyMsgExp, Times.Exactly(2));
            handler2.Verify(handleAnyMsgExp, Times.Never());
            handlerF.Verify(handleAnyMsgExp, Times.Never());

            await dispatcher.DispatchAsync(msg2);

            handler2.Verify(handler => handler.HandleAsync(msg2));
            handler2.Verify(handleAnyMsgExp, Times.Once());
            handler1.Verify(handleAnyMsgExp, Times.Exactly(2));
            handlerF.Verify(handleAnyMsgExp, Times.Never());

            await dispatcher.DispatchAsync(msg3);

            handlerF.Verify(handler => handler.HandleAsync(msg3), "Fallback handler didn't handle a message.");
            handlerF.Verify(handleAnyMsgExp, Times.Once());
            handler1.Verify(handleAnyMsgExp, Times.Exactly(2));
            handler2.Verify(handleAnyMsgExp, Times.Once);
        }
    }
}
