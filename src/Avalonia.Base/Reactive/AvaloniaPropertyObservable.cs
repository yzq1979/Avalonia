using System;
using Avalonia.Data;

namespace Avalonia.Reactive
{
    internal class AvaloniaPropertyObservable<T> : LightweightObservableBase<T>,
        IDescription,
        IObserver<BindingValue<T>>
    {
        private readonly IObservable<BindingValue<T>> _listener;
        private IDisposable _subscription;
        private T _value;

        public AvaloniaPropertyObservable(IObservable<BindingValue<T>> listener)
        {
            _listener = listener;
        }

        public string Description => ((IDescription)_listener).Description;

        void IObserver<BindingValue<T>>.OnCompleted() => PublishCompleted();
        void IObserver<BindingValue<T>>.OnError(Exception error) => PublishError(error);
        
        void IObserver<BindingValue<T>>.OnNext(BindingValue<T> value)
        {
            _value = value.Value;
            PublishNext(_value);
        }

        protected override void Initialize()
        {
            _subscription = _listener.Subscribe(this);
        }

        protected override void Deinitialize()
        {
            _subscription.Dispose();
            _subscription = null;
        }

        protected override void Subscribed(IObserver<T> observer, bool first)
        {
            if (!first)
            {
                observer.OnNext(_value);
            }
        }
    }
}
