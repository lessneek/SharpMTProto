//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging
{
    using System;
    using SharpMTProto.Annotations;
    using SharpMTProto.Schema;

    public interface IMessageEnvelope
    {
        /// <summary>
        ///     MTProto session tag.
        /// </summary>
        MTProtoSessionTag SessionTag { get; }

        /// <summary>
        ///     Server Salt is a (random) 64-bit number periodically (say, every 24 hours) changed (separately for
        ///     each session) at the request of the server. All subsequent messages must contain the new salt (although, messages
        ///     with the old salt are still accepted for a further 300 seconds). Required to protect against replay attacks and
        ///     certain tricks associated with adjusting the client clock to a moment in the distant future.
        /// </summary>
        ulong Salt { get; }

        /// <summary>
        ///     Message.
        /// </summary>
        IMessage Message { get; }

        /// <summary>
        ///     Is message encrypted.
        /// </summary>
        bool IsEncrypted { get; }
    }

    public class MessageEnvelope : IEquatable<MessageEnvelope>, IMessageEnvelope
    {
        /// <summary>
        ///     Initializes a new <see cref="MessageEnvelope" /> for a plain message with zero auth key id.
        /// </summary>
        /// <param name="message">A message.</param>
        private MessageEnvelope([NotNull] IMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            SessionTag = MTProtoSessionTag.Empty;
            Salt = 0;
            Message = message;
        }

        /// <summary>
        ///     Initializes a new <see cref="MessageEnvelope" /> for an encrypted message.
        /// </summary>
        /// <param name="sessionTag">MTProto session tag.</param>
        /// <param name="salt">Salt.</param>
        /// <param name="message">A message.</param>
        private MessageEnvelope(MTProtoSessionTag sessionTag, ulong salt, [NotNull] IMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (sessionTag.AuthKeyId == 0)
                throw new ArgumentOutOfRangeException("sessionTag", "AuthKeyId can not be zero for encrypted message envelope.");

            SessionTag = sessionTag;
            Salt = salt;
            Message = message;
        }

        public static MessageEnvelope CreatePlain(IMessage message)
        {
            return new MessageEnvelope(message);
        }

        public static MessageEnvelope CreateEncrypted(MTProtoSessionTag sessionTag, ulong salt, [NotNull] IMessage message)
        {
            return new MessageEnvelope(sessionTag, salt, message);
        }

        public MTProtoSessionTag SessionTag { get; private set; }
        public ulong Salt { get; private set; }
        public IMessage Message { get; private set; }

        public bool IsEncrypted
        {
            get { return SessionTag.AuthKeyId != 0; }
        }

        #region Equality

        public bool Equals(MessageEnvelope other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return SessionTag == other.SessionTag && Salt == other.Salt && Equals(Message, other.Message);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((MessageEnvelope) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = SessionTag.GetHashCode();
                hashCode = (hashCode*397) ^ Salt.GetHashCode();
                hashCode = (hashCode*397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(MessageEnvelope left, MessageEnvelope right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MessageEnvelope left, MessageEnvelope right)
        {
            return !Equals(left, right);
        }

        #endregion
    }

    public static class MessageEnvelopeExtensions
    {
        public static bool IsEncryptedAndContentRelated(this IMessageEnvelope messageEnvelope)
        {
            return messageEnvelope.IsEncrypted && messageEnvelope.Message.IsContentRelated();
        }
    }
}
