// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMessageHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    /// <summary>
    ///     Message handler.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        ///     Message type.
        /// </summary>
        Type MessageType { get; }

        /// <summary>
        ///     Handle message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Completiong task.</returns>
        Task HandleAsync(IMessage message);
    }
}
