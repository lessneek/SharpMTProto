// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageContainerHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Catel.Logging;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    public class MessageContainerHandler : MessageHandler<IMessageContainer>
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IMessageDispatcher _dispatcher;

        public MessageContainerHandler(IMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        protected override async Task HandleInternalAsync(IMessage message)
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

            var msgContainer = message.Body as MsgContainer;
            if (msgContainer != null)
            {
                if (msgContainer.Messages.Any(msg => msg.MsgId >= message.MsgId || msg.Seqno > message.Seqno))
                {
                    throw new InvalidMessageException("Container MessageId must be greater than all MsgIds of inner messages.");
                }
                foreach (Message msg in msgContainer.Messages)
                {
                    await _dispatcher.DispatchAsync(msg);
                }
            }
            else
            {
                Log.Debug("Unsupported message container of type: {0}.", message.Body.GetType());
            }
        }
    }
}
