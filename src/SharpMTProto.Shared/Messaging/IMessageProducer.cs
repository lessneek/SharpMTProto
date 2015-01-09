//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMethodReturnValue.Global

#endregion

namespace SharpMTProto.Messaging
{
    using System;
    using System.Reactive.Linq;
    using SharpMTProto.Schema;

    public interface IMessageProducer
    {
        IDisposable Subscribe(Action<IMessageEnvelope> action);
    }

    public static class MessageProducerExtensions
    {
        public static IObservable<IMessageEnvelope> AsObservable(this IMessageProducer messageProducer)
        {
            return Observable.Create<IMessageEnvelope>(observer => messageProducer.Subscribe(observer.OnNext));
        }

        public static IDisposable Subscribe(this IMessageProducer messageProducer, IMessageHandler handler)
        {
            return messageProducer.Subscribe(handler.Handle);
        }

        public static IMessageProducer AsMessageProducer(this IObservable<IMessageEnvelope> observable)
        {
            return new AnonymousMessageProducer(observable);
        }

        private class AnonymousMessageProducer : IMessageProducer
        {
            private readonly IObservable<IMessageEnvelope> _observable;

            public AnonymousMessageProducer(IObservable<IMessageEnvelope> observable)
            {
                _observable = observable;
            }

            public IDisposable Subscribe(Action<IMessageEnvelope> action)
            {
                return _observable.Subscribe(action);
            }
        }
    }
}
