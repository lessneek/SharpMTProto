// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    public abstract class MessageHandler<TMessage> : IMessageHandler where TMessage : class
    {
        private static readonly Type MessageTypeInternal = typeof(TMessage);

        public virtual Type MessageType
        {
            get { return MessageTypeInternal; }
        }

        public Task HandleAsync(IMessage message)
        {
            var response = message.Body as TMessage;
            if (response == null)
            {
                throw new MTProtoException(string.Format("Expected message type to be '{0}', but found '{1}'.", MessageTypeInternal, message.Body.GetType()));
            }
            return HandleInternalAsync(message);
        }

        protected abstract Task HandleInternalAsync(IMessage message);
    }
}
