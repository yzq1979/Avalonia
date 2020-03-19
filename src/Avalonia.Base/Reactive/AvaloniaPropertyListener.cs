using System;
using System.Collections.Generic;
using Avalonia.Collections.Pooled;
using Avalonia.Data;
using Avalonia.Threading;

#nullable enable

namespace Avalonia.Reactive
{
    internal abstract class AvaloniaPropertyListener<T> :
        IObservable<AvaloniaPropertyChange<T>>,
        IObservable<BindingValue<T>>,
        IObservable<T>,
        IDescription
    {
        private readonly WeakReference<IAvaloniaObject> _owner;
        private PooledQueue<AvaloniaPropertyChange<T>>? _queue;
        private object? _observer;
        private bool _isSignalling;

        private AvaloniaPropertyListener(IAvaloniaObject owner)
        {
            owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _owner = new WeakReference<IAvaloniaObject>(owner);
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

        public static AvaloniaPropertyListener<T> Create(AvaloniaObject o, StyledPropertyBase<T> property)
        {
            return new Styled(o, property);
        }

        public static AvaloniaPropertyListener<T> Create(AvaloniaObject o, DirectPropertyBase<T> property)
        {
            return new Direct(o, property);
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
            var result = SubscribeCore(observer);
            var value = GetValue();

            if (value.HasValue)
            {
                observer.OnNext(value.Value);
            }

            return result;
        }

        public IDisposable Subscribe(IObserver<AvaloniaPropertyChange<T>> observer)
        {
            var result = SubscribeCore(observer);

            if (_owner.TryGetTarget(out var owner))
            {
                var value = GetValue(owner);
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
            var result = SubscribeCore(observer);
            var value = GetValue();

            if (value.HasValue)
            {
                observer.OnNext(value.Value);
            }

            return result;
        }

        protected abstract T GetValue(IAvaloniaObject owner);

        private void SignalCore(AvaloniaPropertyChange<T> change)
        {
            if (_observer is List<object> list)
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
            else if (_observer is object)
            {
                SignalCore(_observer, change);
            }
        }

        private void SignalCore(object observer, AvaloniaPropertyChange<T> change)
        {
            if (observer is IObserver<AvaloniaPropertyChange<T>> apc)
            {
                apc.OnNext(change);
            }
            else if (!change.IsOutdated)
            {
                if (observer is IObserver<BindingValue<T>> bv)
                {
                    bv.OnNext(change.NewValue);
                }
                else if (observer is IObserver<T> t)
                {
                    t.OnNext(change.NewValue.Value);
                }
            }
        }

        private Optional<T> GetValue()
        {
            if (_owner.TryGetTarget(out var owner))
            {
                return GetValue(owner);
            }

            return default;
        }

        private Disposable SubscribeCore(object observer)
        {
            observer = observer ?? throw new ArgumentNullException(nameof(observer));

            Dispatcher.UIThread.VerifyAccess();

            if (_observer is null)
            {
                _observer = observer;
            }
            else
            {
                if (!(_observer is List<object> list))
                {
                    _observer = list = new List<object> { _observer };
                }

                list.Add(observer);
            }

            return new Disposable(this, observer);
        }

        private void Remove(object observer)
        {
            if (_observer == observer)
            {
                _observer = null;
            }
            else if (_observer is List<object> list)
            {
                list.Remove(observer);
            }
        }

        public class Disposable : IDisposable
        {
            private readonly AvaloniaPropertyListener<T> _owner;
            private readonly object _observer;

            public Disposable(AvaloniaPropertyListener<T> owner, object observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                _owner.Remove(_observer);
            }
        }

        private class Styled : AvaloniaPropertyListener<T>
        {
            public Styled(IAvaloniaObject owner, StyledPropertyBase<T> property)
                : base(owner)
            {
                Property = property ?? throw new ArgumentNullException(nameof(property));
            }

            public override AvaloniaProperty<T> Property { get; }
            protected override T GetValue(IAvaloniaObject owner) => owner.GetValue(Property);
        }

        private class Direct : AvaloniaPropertyListener<T>
        {
            public Direct(IAvaloniaObject owner, DirectPropertyBase<T> property)
                : base(owner)
            {
                Property = property ?? throw new ArgumentNullException(nameof(property));
            }

            public override AvaloniaProperty<T> Property { get; }
            protected override T GetValue(IAvaloniaObject owner) => owner.GetValue(Property);
        }
    }
}
