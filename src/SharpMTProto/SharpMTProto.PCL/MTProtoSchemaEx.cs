// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoSchemaEx.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using SharpMTProto.Messages;
using SharpTL;

// ReSharper disable once CheckNamespace

namespace MTProtoSchema
{
    [TLObject(typeof (MessageSerializer))]
    public class Message : IMessage
    {
        public Message()
        {
        }

        public Message(ulong msgId, uint seqno, object body)
        {
            MsgId = msgId;
            Seqno = seqno;
            Body = body;
        }

        public UInt64 MsgId { get; set; }

        public UInt32 Seqno { get; set; }

        public Object Body { get; set; }
    }

    public partial interface IMessage
    {
        /// <summary>
        ///     Message Identifier is a (time-dependent) 64-bit number used uniquely to identify a message within a session. Client
        ///     message identifiers are divisible by 4, server message identifiers modulo 4 yield 1 if the message is a response to
        ///     a client message, and 3 otherwise. Client message identifiers must increase monotonically (within a single
        ///     session), the same as server message identifiers, and must approximately equal unixtime*2^32. This way, a message
        ///     identifier points to the approximate moment in time the message was created. A message is rejected over 300 seconds
        ///     after it is created or 30 seconds before it is created (this is needed to protect from replay attacks). In this
        ///     situation, it must be re-sent with a different identifier (or placed in a container with a higher identifier). The
        ///     identifier of a message container must be strictly greater than those of its nested messages.
        /// </summary>
        UInt64 MsgId { get; }

        /// <summary>
        ///     Message Sequence Number is a 32-bit number equal to twice the number of “content-related” messages (those requiring
        ///     acknowledgment, and in particular those that are not containers) created by the sender prior to this message and
        ///     subsequently incremented by one if the current message is a content-related message. A container is always
        ///     generated after its entire contents; therefore, its sequence number is greater than or equal to the sequence
        ///     numbers of the messages contained in it.
        /// </summary>
        UInt32 Seqno { get; }

        Object Body { get; }
    }

    public partial interface IRpcResult
    {
        UInt64 ReqMsgId { get; set; }

        Object Result { get; set; }
    }
}
