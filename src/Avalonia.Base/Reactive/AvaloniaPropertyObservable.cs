using System;
using System.Collections.Generic;
using Avalonia.Collections.Pooled;
using Avalonia.Data;
using Avalonia.Threading;

#nullable enable

namespace Avalonia.Reactive
{
    internal abstract class AvaloniaPropertyObservable<T> :
        IObservable<AvaloniaPropertyChange<T>>,
        IObservable<BindingValue<T>>,
        IObservable<T>,
        IDescription
    {
        private readonly WeakReference<AvaloniaObject> _owner;
        private NonAnimatedProxy? _nonAnimated;
        private PooledQueue<AvaloniaPropertyChange<T>>? _queue;
        private object? _observer;
        private bool _isSignalling;

        private AvaloniaPropertyObservable(AvaloniaObject owner)
        {
            owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _owner = new WeakReference<AvaloniaObject>(owner);
        }

        public string Description
        {
            get
            {
                if (_owner.TryGetTarget(out var owner))
                {
                    return $"{owner.GetType().Name}.{Property.Name}";
                }
                else
                {
                    return $"(dead).{Property.Name}";
                }
            }
        }

        public abstract AvaloniaProperty<T> Property { get; }

        public IObservable<AvaloniaPropertyChange<T>> NonAnimated => _nonAnimated ??= new NonAnimatedProxy(this);

        public static AvaloniaPropertyObservable<T> Create(AvaloniaObject o, StyledPropertyBase<T> property)
        {
            return new Styled(o, property);
        }

        public static AvaloniaPropertyObservable<T> Create(AvaloniaObject o, DirectPropertyBase<T> property)
        {
            return new Direct(o, property);
        }

        public IObservable<AvaloniaPropertyChange<T>> Get(bool includeAnimations)
        {
            return includeAnimations ? this : NonAnimated;
        }

        public void Signal(AvaloniaPropertyChange<T> change)
        {
            if (!_isSignalling)
            {
                _isSignalling = true;
                SignalCore(change);

                if (_queue is object)
                {
                    while (_queue.TryDequeue(out change))
                    {
                        SignalCore(change);
                    }

                    _queue.Dispose();
                }

                _isSignalling = false;
            }
            else
            {
                _queue ??= new PooledQueue<AvaloniaPropertyChange<T>>();
                _queue.Enqueue(change);
            }
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var result = SubscribeCore(observer, true);
            var value = GetValue(true);

            if (value.HasValue)
            {
                observer.OnNext(value.Value);
            }

            return result;
        }

        public IDisposable Subscribe(IObserver<AvaloniaPropertyChange<T>> observer)
        {
            return Subscribe(observer, true);
        }

        public IDisposable Subscribe(
            IObserver<AvaloniaPropertyChange<T>> observer,
            bool includeAnimations)
        {
            var result = SubscribeCore(observer, includeAnimations);

            if (_owner.TryGetTarget(out var owner))
            {
                var value = GetValue(owner, includeAnimations);
                var change = new AvaloniaPropertyChange<T>(
                    owner,
                    Property,
                    default,
                    value,
                    BindingPriority.Unset);
                observer.OnNext(change);
            }

            return result;
        }

        public IDisposable Subscribe(IObserver<BindingValue<T>> observer)
        {
            var result = SubscribeCore(observer, true);
            var value = GetValue(true);

            if (value.HasValue)
            {
                observer.OnNext(value.Value);
            }

            return result;
        }

        protected abstract T GetValue(AvaloniaObject owner, bool includeAnimations);

        private void SignalCore(AvaloniaPropertyChange<T> change)
        {
            if (_observer is List<ObserverEntry> list)
            {
                foreach (var observer in list)
                {
                    if (_queue?.Count > 0 && !change.IsOutdated)
                    {
                        change = change.MakeOutdated();
                    }

                    SignalCore(observer, change);
                }
            }
            else if (_observer is ObserverEntry e)
            {
                SignalCore(e, change);
            }
        }

        private void SignalCore(ObserverEntry entry, AvaloniaPropertyChange<T> change)
        {
            if (entry.Observer is IObserver<AvaloniaPropertyChange<T>> apc)
            {
                if (entry.IncludeAnimations)
                {
                    if (change.IsActiveValueChange)
                    {
                        apc.OnNext(change);
                    }
                }
                else
                {
                    if (change.Priority > BindingPriority.Animation)
                    {
                        apc.OnNext(change);
                    }
                }
            }
            else if (!change.IsOutdated && change.IsActiveValueChange)
            {
                if (entry.Observer is IObserver<BindingValue<T>> bv)
                {
                    bv.OnNext(change.NewValue);
                }
                else if (entry.Observer is IObserver<T> t)
                {
                    t.OnNext(change.NewValue.Value);
                }
            }
        }

        private Optional<T> GetValue(bool includeAnimations)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                return GetValue(owner, includeAnimations);
            }

            return default;
        }

        private IDisposable SubscribeCore(object observer, bool includeAnimations)
        {
            observer = observer ?? throw new ArgumentNullException(nameof(observer));

            Dispatcher.UIThread.VerifyAccess();

            if (_observer is null)
            {
                _observer = new ObserverEntry(observer, includeAnimations);
            }
            else
            {
                if (!(_observer is List<ObserverEntry> list))
                {
                    var existing = (ObserverEntry)_observer;
                    _observer = list = new List<ObserverEntry>();
                    list.Add(existing);
                }

                list.Add(new ObserverEntry(observer, includeAnimations));
            }

            return new Disposable(this, observer);
        }

        private void Remove(object observer)
        {
            if (_observer is ObserverEntry e && e.Observer == observer)
            {
                _observer = null;
            }
            else if (_observer is List<ObserverEntry> list)
            {
                for (var i = 0; i < list.Count; ++i)
                {
                    if (list[i].Observer == observer)
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private struct ObserverEntry
        {
            public ObserverEntry(object observer, bool includeAnimations)
            {
                Observer = observer;
                IncludeAnimations = includeAnimations;
            }

            public object Observer { get; }
            public bool IncludeAnimations { get; }
        }

        private class Disposable : IDisposable
        {
            private readonly AvaloniaPropertyObservable<T> _owner;
            private readonly object _observer;

            public Disposable(AvaloniaPropertyObservable<T> owner, object observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                _owner.Remove(_observer);
            }
        }

        private class NonAnimatedProxy : IObservable<AvaloniaPropertyChange<T>>
        {
            private readonly AvaloniaPropertyObservable<T> _owner;

            public NonAnimatedProxy(AvaloniaPropertyObservable<T> owner) => _owner = owner;

            public IDisposable Subscribe(IObserver<AvaloniaPropertyChange<T>> observer)
            {
                return _owner.Subscribe(observer, false);
            }
        }

        private class Styled : AvaloniaPropertyObservable<T>
        {
            private readonly StyledPropertyBase<T> _property;

            public Styled(AvaloniaObject owner, StyledPropertyBase<T> property)
                : base(owner)
            {
                _property = property ?? throw new ArgumentNullException(nameof(property));
            }

            public override AvaloniaProperty<T> Property => _property;
            
            protected override T GetValue(AvaloniaObject owner, bool includeAnimations)
            {
                return includeAnimations ?
                    owner.GetValue(_property) :
                    owner.GetAnimationBaseValue(_property);
            }
        }

        private class Direct : AvaloniaPropertyObservable<T>
        {
            private readonly DirectPropertyBase<T> _property;

            public Direct(AvaloniaObject owner, DirectPropertyBase<T> property)
                : base(owner)
            {
                _property = property ?? throw new ArgumentNullException(nameof(property));
            }

            public override AvaloniaProperty<T> Property => _property;

            protected override T GetValue(AvaloniaObject owner, bool includeAnimations)
            {
                return owner.GetValue(_property);
            }
        }
    }
}
