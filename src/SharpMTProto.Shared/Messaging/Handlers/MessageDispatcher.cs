// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageDispatcher.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catel.Logging;
using Catel.Reflection;
using Nito.AsyncEx;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    /// <summary>
    ///     Message dispatcher.
    /// </summary>
    public interface IMessageDispatcher
    {
        /// <summary>
        ///     Fallback handler.
        ///     If it is set then all messages without handler are handled by this handler, otherwise message is ignored.
        /// </summary>
        IMessageHandler FallbackHandler { get; set; }

        /// <summary>
        ///     Dispatch message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Task.</returns>
        Task DispatchAsync(IMessage message);

        /// <summary>
        ///     Add handler for a message of a type which is set in handler's <see cref="IMessageHandler.MessageType" />
        ///     property.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="overwriteExisted">Overwrite existed.</param>
        void AddHandler(IMessageHandler handler, bool overwriteExisted = false);

        /// <summary>
        ///     Add handler for a response of specified type.
        ///     Handler's <see cref="IMessageHandler.MessageType" /> property do not affect anything.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="overwriteExisted">Overwrite existed.</param>
        void AddHandler<TResponse>(IMessageHandler handler, bool overwriteExisted = false);

        /// <summary>
        ///     Add handler for a message.
        ///     Handler's <see cref="IMessageHandler.MessageType" /> property do not affect anything.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="messageType">Message type.</param>
        /// <param name="overwriteExisted">Overwrite existed.</param>
        void AddHandler(IMessageHandler handler, Type messageType, bool overwriteExisted = false);
    }
    
    /// <summary>
    ///     Message dispatcher routes messages to proper message handler.
    /// </summary>
    public class MessageDispatcher : IMessageDispatcher
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<Type, IMessageHandler> _handlers = new Dictionary<Type, IMessageHandler>();

        public IMessageHandler FallbackHandler { get; set; }

        public async Task DispatchAsync(IMessage message)
        {
            Type messageType = message.Body.GetType();

            IMessageHandler handler = _handlers.Where(pair => pair.Key.IsAssignableFromEx(messageType)).Select(pair => pair.Value).FirstOrDefault();
            if (handler == null)
            {
                if (FallbackHandler != null)
                {
                    handler = FallbackHandler;
                }
                else
                {
                    Log.WarningWithData(
                        string.Format("No handler found for message of type '{0}' and there is no fallback handler. Message was ignored.", messageType.Name));
                    return;
                }
            }

            try
            {
                await handler.HandleAsync(message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while handling a message.");
            }
        }

        public void AddHandler(IMessageHandler handler, bool overwriteExisted = false)
        {
            AddHandler(handler, handler.MessageType, overwriteExisted);
        }

        public void AddHandler<TMessage>(IMessageHandler handler, bool overwriteExisted = false)
        {
            Type messageType = typeof(TMessage);
            AddHandler(handler, messageType, overwriteExisted);
        }

        public void AddHandler(IMessageHandler handler, Type messageType, bool overwriteExisted = false)
        {
            if (_handlers.ContainsKey(messageType))
            {
                if (!overwriteExisted)
                {
                    Log.Warning(string.Format("Prevented addition of another handler '{0}' for message of type '{1}'.", handler.GetType(), handler.MessageType.Name));
                    return;
                }
                _handlers.Remove(messageType);
            }

            _handlers.Add(messageType, handler);
        }
    }
}
