// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageSending.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using SharpMTProto.Schema;

namespace SharpMTProto
{
    [Flags]
    public enum MessageSendingFlags
    {
        None = 0,
        Encrypted = 1,
        ContentRelated = 1 << 1,
        EncryptedAndContentRelated = Encrypted | ContentRelated
    }

    public class MessageSending
    {
        public MessageSending(IMessage message, MessageSendingFlags flags)
        {
            Message = message;
            Flags = flags;
        }

        public IMessage Message { get; private set; }

        public MessageSendingFlags Flags { get; private set; }

        public bool IsAcknowledged { get; private set; }

        public bool IsSent { get; private set; }

        /// <summary>
        ///     Acknowledge UTC date time.
        /// </summary>
        public DateTime AcknowledgeTime { get; private set; }

        /// <summary>
        ///     Sending UTC date time.
        /// </summary>
        public DateTime SendingTime { get; private set; }

        public void Sent()
        {
            if (IsSent)
            {
                return;
            }
            IsSent = true;
            SendingTime = DateTime.UtcNow;
        }

        public void Acknowledge()
        {
            if (!IsSent)
            {
                throw new InvalidOperationException("Could not be acknowledged before message is sent.");
            }
            if (IsAcknowledged)
            {
                return;
            }
            IsAcknowledged = true;
            AcknowledgeTime = DateTime.UtcNow;
        }
    }
}
