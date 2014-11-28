// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FirstRequestResponseHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Catel;
using Catel.Logging;
using SharpMTProto.Schema;

namespace SharpMTProto.Messaging.Handlers
{
    public class FirstRequestResponseHandler : IMessageHandler
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly Type ResponseTypeInternal = typeof (object);
        private readonly IRequestsManager _requestsManager;

        public FirstRequestResponseHandler(IRequestsManager requestsManager)
        {
            _requestsManager = requestsManager;
        }

        public Type MessageType
        {
            get { return ResponseTypeInternal; }
        }

        /// <exception cref="System.ArgumentNullException">The <paramref name="message" /> is <c>null</c>.</exception>
        public Task HandleAsync(IMessage message)
        {
            Argument.IsNotNull(() => message);
            return Task.Run(() =>
            {
                IRequest request = _requestsManager.GetFirstOrDefault(message.Body);
                if (request == null)
                {
                    Log.Warning(string.Format("Request for response of type '{0}' not found.", message.Body.GetType()));
                    return;
                }

                request.SetResponse(message.Body);
            });
        }
    }
}
