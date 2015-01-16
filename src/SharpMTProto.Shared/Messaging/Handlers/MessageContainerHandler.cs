//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Linq;
    using System.Reactive.Subjects;
    using SharpMTProto.Schema;
    using SharpMTProto.Utils;

    public class MessageContainerHandler : SingleMessageHandler<IMessageContainer>, IObservable<IMessageEnvelope>
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private Subject<IMessageEnvelope> _messageHandler = new Subject<IMessageEnvelope>();

        public IDisposable Subscribe(IObserver<IMessageEnvelope> observer)
        {
            return _messageHandler.Subscribe(observer);
        }

        protected override void HandleInternal(IMessageEnvelope messageEnvelope)
        {
            #region Description

            /*
             * All messages in a container must have msg_id lower than that of the container itself.
             * A container does not require an acknowledgment and may not carry other simple containers.
             * When messages are re-sent, they may be combined into a container in a different manner or sent individually.
             * 
             * Empty containers are also allowed. They are used by the server, for example,
             * to respond to an HTTP request when the timeout specified in hhtp_wait expires, and there are no messages to transmit.
             * 
             * https://core.telegram.org/mtproto/service_messages#containers
             */

            #endregion

            IMessage message = messageEnvelope.Message;
            var msgContainer = message.Body as MsgContainer;
            if (msgContainer != null)
            {
                if (msgContainer.Messages.Any(msg => msg.MsgId >= message.MsgId || msg.Seqno > message.Seqno))
                {
                    throw new InvalidMessageException("Container MessageId must be greater than all MsgIds of inner messages.");
                }
                foreach (Message msg in msgContainer.Messages)
                {
                    _messageHandler.OnNext(new MessageEnvelope(messageEnvelope.SessionTag, messageEnvelope.Salt, msg));
                }
            }
            else
            {
                Log.Debug(string.Format("Unsupported message container of type: {0}.", message.Body.GetType()));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_messageHandler != null)
                {
                    _messageHandler.OnCompleted();
                    _messageHandler.Dispose();
                    _messageHandler = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
