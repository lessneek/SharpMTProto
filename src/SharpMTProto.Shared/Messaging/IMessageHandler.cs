//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMemberInSuper.Global

#endregion

namespace SharpMTProto.Messaging
{
    using System.Threading.Tasks;
    using SharpMTProto.Schema;

    /// <summary>
    ///     Message handler.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        ///     Handles a message asynchronously.
        /// </summary>
        /// <param name="messageEnvelope">A messageEnvelope to handle.</param>
        Task HandleAsync(IMessageEnvelope messageEnvelope);

        /// <summary>
        ///     Handles a message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope to handle.</param>
        void Handle(IMessageEnvelope messageEnvelope);
    }
}
