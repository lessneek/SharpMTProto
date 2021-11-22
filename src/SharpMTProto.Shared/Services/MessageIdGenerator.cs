// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageIdGenerator.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using SharpMTProto.Utils;

    /// <summary>
    ///     Interface for a message ID generator.
    /// </summary>
    public interface IMessageIdGenerator
    {
        ulong GetNextMessageId();
        ulong GetNextServerMessageId(bool isResponse);
    }

    /// <summary>
    ///     The default MTProto message ID generator.
    /// </summary>
    public class MessageIdGenerator : IMessageIdGenerator
    {
        /* Message Identifier (msg_id)
         * a (time-dependent) 64-bit number used uniquely to identify a message within a session.
         * Client message identifiers are divisible by 4,
         * server message identifiers modulo 4 yield 1 if the message is a response to a client message, and 3 otherwise.
         * Client message identifiers must increase monotonically (within a single session),
         * the same as server message identifiers, and must approximately equal unixtime*2^32.
         * This way, a message identifier points to the approximate moment in time the message was created.
         * A message is rejected over 300 seconds after it is created or 30 seconds before it is created (this is needed to protect from replay attacks).
         * In this situation, it must be re-sent with a different identifier (or placed in a container with a higher identifier).
         * The identifier of a message container must be strictly greater than those of its nested messages.
         *
         * Important: to counter replay-attacks the lower 32 bits of msg_id passed by the client must not be empty and must present
         * a fractional part of the time point when the message was created.
         * At some point in the nearest future the server will start ignoring messages, in which the lower 32 bits of msg_id contain too many zeroes.
         *
         * https://core.telegram.org/mtproto/description#message-identifier-msg-id
         */

        private const ulong X4Mask = ~3UL;
        private readonly object _sync = new object();
        private ulong _lastMessageId;

        public ulong GetNextMessageId()
        {
            ulong messageId = UnixTimeUtils.GetCurrentUnixTimestampMilliseconds();
            messageId = (messageId*4294967 + (messageId*296/1000)) & X4Mask;
            lock (_sync)
            {
                if (messageId <= _lastMessageId)
                    messageId = _lastMessageId + 4;

                _lastMessageId = messageId;
            }
            return messageId;
        }

        public ulong GetNextServerMessageId(bool isResponse = true)
        {
            ulong messageId = GetNextMessageId();
            messageId = isResponse ? messageId + 1 : messageId + 3;
            return messageId;
        }
    }
}
