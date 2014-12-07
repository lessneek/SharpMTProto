// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FirstRequestResponseHandler.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging.Handlers
{
    using System;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Catel;
    using Catel.Logging;
    using Schema;

    public class FirstRequestResponseHandler : MessageHandler
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IRequestsManager _requestsManager;
        private IDisposable _messageTypesSubscription;

        public FirstRequestResponseHandler(IRequestsManager requestsManager, IObservable<ImmutableArray<Type>> messageTypesObservable)
        {
            _requestsManager = requestsManager;
            _messageTypesSubscription = messageTypesObservable.Subscribe(types => MessageTypes = types);
        }

        public override Task HandleAsync(IMessage message)
        {
            Argument.IsNotNull(() => message);
            return Task.Run(() =>
            {
                IRequest request = _requestsManager.GetFirstOrDefaultWithUnsetResponse(message.Body);
                if (request == null)
                {
                    Log.Warning(string.Format("Request for response of type '{0}' not found.", message.Body.GetType()));
                    return;
                }

                request.SetResponse(message.Body);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_messageTypesSubscription != null)
                {
                    _messageTypesSubscription.Dispose();
                    _messageTypesSubscription = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
