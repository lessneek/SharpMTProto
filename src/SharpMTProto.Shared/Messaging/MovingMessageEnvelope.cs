//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Messaging
{
    using System;
    using SharpMTProto.Transport;

    public struct MovingMessageEnvelope : IEquatable<MovingMessageEnvelope>
    {
        public MovingMessageEnvelope(IClientTransport clientTransport, IMessageEnvelope messageEnvelope) : this()
        {
            ClientTransport = clientTransport;
            MessageEnvelope = messageEnvelope;
        }

        public IClientTransport ClientTransport { get; private set; }
        public IMessageEnvelope MessageEnvelope { get; private set; }

        #region Equality

        public bool Equals(MovingMessageEnvelope other)
        {
            return Equals(ClientTransport, other.ClientTransport) && Equals(MessageEnvelope, other.MessageEnvelope);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is MovingMessageEnvelope && Equals((MovingMessageEnvelope) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ClientTransport != null ? ClientTransport.GetHashCode() : 0)*397) ^
                    (MessageEnvelope != null ? MessageEnvelope.GetHashCode() : 0);
            }
        }

        public static bool operator ==(MovingMessageEnvelope left, MovingMessageEnvelope right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MovingMessageEnvelope left, MovingMessageEnvelope right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}
