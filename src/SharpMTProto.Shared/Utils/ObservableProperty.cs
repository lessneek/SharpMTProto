//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMemberInSuper.Global

#endregion

namespace SharpMTProto.Utils
{
    using System;
    using System.Reactive.Disposables;
    using System.Reactive.Subjects;

    /// <summary>
    ///     Observable property.
    /// </summary>
    /// <typeparam name="TOwner">Type of the owner.</typeparam>
    /// <typeparam name="TProperty">Type of the property.</typeparam>
    public interface IObservableProperty<TOwner, TProperty> : IObservable<PropertyChange<TOwner, TProperty>>, ICancelable
    {
        TProperty Value { get; set; }
        IObservableReadonlyProperty<TOwner, TProperty> AsReadonly { get; }
    }

    /// <summary>
    ///     Readonly wrapper around observable property.
    /// </summary>
    /// <typeparam name="TOwner">Type of the owner.</typeparam>
    /// <typeparam name="TProperty">Type of the property.</typeparam>
    public interface IObservableReadonlyProperty<TOwner, TProperty> : IObservable<PropertyChange<TOwner, TProperty>>, ICancelable
    {
        TProperty Value { get; }
    }

    /// <summary>
    ///     Observable property.
    /// </summary>
    /// <typeparam name="TOwner">Type of the owner.</typeparam>
    /// <typeparam name="TProperty">Type of the property.</typeparam>
    public class ObservableProperty<TOwner, TProperty> : Cancelable, IObservableProperty<TOwner, TProperty>
    {
        private readonly IObservableReadonlyProperty<TOwner, TProperty> _asReadonly;
        private readonly TOwner _owner;
        private Subject<PropertyChange<TOwner, TProperty>> _changes = new Subject<PropertyChange<TOwner, TProperty>>();
        private TProperty _value;

        private ObservableProperty()
        {
            _asReadonly = new ObservableReadonlyProperty<TOwner, TProperty>(this);
        }

        public ObservableProperty(TOwner owner) : this()
        {
            _owner = owner;
        }

        public ObservableProperty(TOwner owner, TProperty initialValue) : this(owner)
        {
            _value = initialValue;
        }

        public IObservableReadonlyProperty<TOwner, TProperty> AsReadonly
        {
            get { return _asReadonly; }
        }

        public IDisposable Subscribe(IObserver<PropertyChange<TOwner, TProperty>> observer)
        {
            return _changes.Subscribe(observer);
        }

        public TProperty Value
        {
            get
            {
                ThrowIfDisposed();

                return _value;
            }
            set
            {
                ThrowIfDisposed();
                TProperty oldValue = _value;
                _value = value;
                _changes.OnNext(new PropertyChange<TOwner, TProperty>(_owner, oldValue, _value));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_changes != null)
                {
                    _changes.OnCompleted();
                    _changes.Dispose();
                    _changes = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    ///     Readonly wrapper around observable property.
    /// </summary>
    /// <typeparam name="TOwner">Type of the owner.</typeparam>
    /// <typeparam name="TProperty">Type of the property.</typeparam>
    public class ObservableReadonlyProperty<TOwner, TProperty> : Cancelable, IObservableReadonlyProperty<TOwner, TProperty>
    {
        private readonly IObservableProperty<TOwner, TProperty> _observableProperty;

        public ObservableReadonlyProperty(IObservableProperty<TOwner, TProperty> observableProperty)
        {
            _observableProperty = observableProperty;
        }

        public IDisposable Subscribe(IObserver<PropertyChange<TOwner, TProperty>> observer)
        {
            return _observableProperty.Subscribe(observer);
        }

        public TProperty Value
        {
            get { return _observableProperty.Value; }
        }
    }

    /// <summary>
    ///     Represents a single property change. Contains an old and a new values of the property, alongside with sender.
    /// </summary>
    /// <typeparam name="TSender"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    public class PropertyChange<TSender, TProperty>
    {
        public PropertyChange(TSender sender, TProperty oldValue, TProperty newValue)
        {
            Sender = sender;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public TSender Sender { get; private set; }
        public TProperty OldValue { get; private set; }
        public TProperty NewValue { get; private set; }
    }
}
