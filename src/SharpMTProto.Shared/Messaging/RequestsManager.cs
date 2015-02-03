// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RequestsManager.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Disposables;
    using Utils;

    public interface IRequestsManager : ICancelable
    {
        void Add(IRequest request);
        IRequest Get(ulong messageId);
        IRequest GetFirstOrDefaultWithUnsetResponse(object response, bool includeRpc = false);
        bool Change(ulong newMessageId, ulong oldMessageId);
        bool Remove(ulong messageId);
    }

    public class RequestsManager : Cancelable, IRequestsManager
    {
        private SortedDictionary<ulong, IRequest> _requests = new SortedDictionary<ulong, IRequest>();

        public void Add(IRequest request)
        {
            ThrowIfDisposed();
            lock (_requests)
            {
                _requests.Add(request.MsgId, request);
            }
        }

        public bool Change(ulong newMessageId, ulong oldMessageId)
        {
            ThrowIfDisposed();
            lock (_requests)
            {
                if (!_requests.ContainsKey(oldMessageId))
                    return false;

                _requests.Add(newMessageId, _requests[oldMessageId]);
                _requests.Remove(oldMessageId);
                return true;
            }
        }

        public IRequest Get(ulong messageId)
        {
            ThrowIfDisposed();
            lock (_requests)
            {
                IRequest request;
                return _requests.TryGetValue(messageId, out request) ? request : null;
            }
        }

        public IRequest GetFirstOrDefaultWithUnsetResponse(object response, bool includeRpc = false)
        {
            ThrowIfDisposed();
            lock (_requests)
            {
                return
                    _requests.Values.FirstOrDefault(
                        r => r.CanSetResponse(response.GetType()) && (!r.Flags.HasFlag(MessageSendingFlags.RPC) || includeRpc));
            }
        }

        public bool Remove(ulong messageId)
        {
            lock (_requests)
            {
                return _requests.Remove(messageId);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_requests)
                {
                    if (_requests != null)
                    {
                        _requests.Clear();
                        _requests = null;
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
