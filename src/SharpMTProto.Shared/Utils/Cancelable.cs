// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Cancelable.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Utils
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reactive.Disposables;
    using System.Threading;

    /// <summary>
    ///     Base class for cancelable objects.
    /// </summary>
    public class Cancelable : ICancelable
    {
        private const int DisposedFlag = 1;
        private int _isDisposed;

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly",
            Justification = "Dispose is implemented correctly, FxCop just doesn't see it.")]
        public void Dispose()
        {
            var wasDisposed = Interlocked.Exchange(ref _isDisposed, DisposedFlag);
            if (wasDisposed == DisposedFlag)
            {
                return;
            }

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
        ///     unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        ///     Returns true if the current instance has been disposed; otherwise false;
        /// </summary>
        public bool IsDisposed
        {
            get
            {
#if !PCL
                Thread.MemoryBarrier();
#endif
                return _isDisposed == DisposedFlag;
            }
        }

        [DebuggerStepThrough]
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
            }
        }
    }
}
