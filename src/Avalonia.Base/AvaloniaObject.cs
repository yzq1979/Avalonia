using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Logging;
using Avalonia.PropertyStore;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace Avalonia
{
    /// <summary>
    /// An object with <see cref="AvaloniaProperty"/> support.
    /// </summary>
    /// <remarks>
    /// This class is analogous to DependencyObject in WPF.
    /// </remarks>
    public class AvaloniaObject : IAvaloniaObject, IAvaloniaObjectDebug, INotifyPropertyChanged, IValueSink
    {
        private IAvaloniaObject _inheritanceParent;
        private List<IDisposable> _directBindings;
        private PropertyChangedEventHandler _inpcChanged;
        private EventHandler<AvaloniaPropertyChangedEventArgs> _propertyChanged;
        private List<IAvaloniaObject> _inheritanceChildren;
        private ValueStore _values;
        private AvaloniaPropertyValueStore<object> _listeners;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaObject"/> class.
        /// </summary>
        public AvaloniaObject()
        {
            VerifyAccess();
        }

        /// <summary>
        /// Raised when a <see cref="AvaloniaProperty"/> value changes on this object.
        /// </summary>
        public event EventHandler<AvaloniaPropertyChangedEventArgs> PropertyChanged
        {
            add { _propertyChanged += value; }
            remove { _propertyChanged -= value; }
        }

        /// <summary>
        /// Raised when a <see cref="AvaloniaProperty"/> value changes on this object.
        /// </summary>
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { _inpcChanged += value; }
            remove { _inpcChanged -= value; }
        }

        /// <summary>
        /// Gets or sets the parent object that inherited <see cref="AvaloniaProperty"/> values
        /// are inherited from.
        /// </summary>
        /// <value>
        /// The inheritance parent.
        /// </value>
        protected IAvaloniaObject InheritanceParent
        {
            get
            {
                return _inheritanceParent;
            }

            set
            {
                VerifyAccess();

                if (_inheritanceParent != value)
                {
                    var oldParent = _inheritanceParent;
                    var valuestore = _values;

                    _inheritanceParent?.RemoveInheritanceChild(this);
                    _inheritanceParent = value;

                    var properties = AvaloniaPropertyRegistry.Instance.GetRegisteredInherited(GetType());
                    var propertiesCount = properties.Count;

                    for (var i = 0; i < propertiesCount; i++)
                    {
                        var property = properties[i];
                        if (valuestore?.IsSet(property) == true)
                        {
                            // If local value set there can be no change.
                            continue;
                        }

                        property.RouteInheritanceParentChanged(this, oldParent);
                    }

                    _inheritanceParent?.AddInheritanceChild(this);
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of a <see cref="AvaloniaProperty"/>.
        /// </summary>
        /// <param name="property">The property.</param>
        public object this[AvaloniaProperty property]
        {
            get { return GetValue(property); }
            set { SetValue(property, value); }
        }

        /// <summary>
        /// Gets or sets a binding for a <see cref="AvaloniaProperty"/>.
        /// </summary>
        /// <param name="binding">The binding information.</param>
        public IBinding this[IndexerDescriptor binding]
        {
            get
            {
                return new IndexerBinding(this, binding.Property, binding.Mode);
            }

            set
            {
                var sourceBinding = value as IBinding;
                this.Bind(binding.Property, sourceBinding);
            }
        }

        private ValueStore Values => _values ?? (_values = new ValueStore(this));

        public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

        public void VerifyAccess() => Dispatcher.UIThread.VerifyAccess();

        /// <summary>
        /// Clears a <see cref="AvaloniaProperty"/>'s local value.
        /// </summary>
        /// <param name="property">The property.</param>
        public void ClearValue(AvaloniaProperty property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));

            property.RouteClearValue(this);
        }

        /// <summary>
        /// Clears a <see cref="AvaloniaProperty"/>'s local value.
        /// </summary>
        /// <param name="property">The property.</param>
        public void ClearValue<T>(AvaloniaProperty<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            switch (property)
            {
                case StyledPropertyBase<T> styled:
                    ClearValue(styled);
                    break;
                case DirectPropertyBase<T> direct:
                    ClearValue(direct);
                    break;
                default:
                    throw new NotSupportedException("Unsupported AvaloniaProperty type.");
            }
        }

        /// <summary>
        /// Clears a <see cref="AvaloniaProperty"/>'s local value.
        /// </summary>
        /// <param name="property">The property.</param>
        public void ClearValue<T>(StyledPropertyBase<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            _values?.ClearLocalValue(property);
        }

        /// <summary>
        /// Clears a <see cref="AvaloniaProperty"/>'s local value.
        /// </summary>
        /// <param name="property">The property.</param>
        public void ClearValue<T>(DirectPropertyBase<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            var p = AvaloniaPropertyRegistry.Instance.GetRegisteredDirect(this, property);
            p.InvokeSetter(this, p.GetUnsetValue(GetType()));
        }

        /// <summary>
        /// Compares two objects using reference equality.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <remarks>
        /// Overriding Equals and GetHashCode on an AvaloniaObject is disallowed for two reasons:
        /// 
        /// - AvaloniaObjects are by their nature mutable
        /// - The presence of attached properties means that the semantics of equality are
        ///   difficult to define
        /// 
        /// See https://github.com/AvaloniaUI/Avalonia/pull/2747 for the discussion that prompted
        /// this.
        /// </remarks>
        public sealed override bool Equals(object obj) => base.Equals(obj);

        /// <summary>
        /// Gets the hash code for the object.
        /// </summary>
        /// <remarks>
        /// Overriding Equals and GetHashCode on an AvaloniaObject is disallowed for two reasons:
        /// 
        /// - AvaloniaObjects are by their nature mutable
        /// - The presence of attached properties means that the semantics of equality are
        ///   difficult to define
        /// 
        /// See https://github.com/AvaloniaUI/Avalonia/pull/2747 for the discussion that prompted
        /// this.
        /// </remarks>
        public sealed override int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Gets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        public object GetValue(AvaloniaProperty property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));

            return property.RouteGetValue(this);
        }

        /// <summary>
        /// Gets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        public T GetValue<T>(StyledPropertyBase<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            return GetValueOrInheritedOrDefault(property);
        }

        /// <summary>
        /// Gets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        public T GetValue<T>(DirectPropertyBase<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            var registered = AvaloniaPropertyRegistry.Instance.GetRegisteredDirect(this, property);
            return registered.InvokeGetter(this);
        }

        /// <summary>
        /// Gets a <see cref="AvaloniaProperty"/> value, disregarding any possible animated value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        public T GetAnimationBaseValue<T>(StyledPropertyBase<T> property)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            return GetValueOrInheritedOrDefault(property, false);
        }

        /// <summary>
        /// Checks whether a <see cref="AvaloniaProperty"/> is animating.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>True if the property is animating, otherwise false.</returns>
        public bool IsAnimating(AvaloniaProperty property)
        {
            Contract.Requires<ArgumentNullException>(property != null);
            VerifyAccess();

            return _values?.IsAnimating(property) ?? false;
        }

        /// <summary>
        /// Checks whether a <see cref="AvaloniaProperty"/> is set on this object.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>True if the property is set, otherwise false.</returns>
        /// <remarks>
        /// Checks whether a value is assigned to the property, or that there is a binding to the
        /// property that is producing a value other than <see cref="AvaloniaProperty.UnsetValue"/>.
        /// </remarks>
        public bool IsSet(AvaloniaProperty property)
        {
            Contract.Requires<ArgumentNullException>(property != null);
            VerifyAccess();

            return _values?.IsSet(property) ?? false;
        }

        /// <summary>
        /// Gets a listener for an <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="includeAnimations">
        /// Whether to include property changes caused by animations.
        /// </param>
        /// <returns>The listener observable.</returns>
        public IObservable<AvaloniaPropertyChange<T>> Listen<T>(
            StyledPropertyBase<T> property,
            bool includeAnimations = true)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));

            _listeners ??= new AvaloniaPropertyValueStore<object>();

            if (_listeners.TryGetValue(property, out var existing))
            {
                return ((AvaloniaPropertyListener<T>)existing).Get(includeAnimations);
            }

            var listener = AvaloniaPropertyListener<T>.Create(this, property);
            _listeners.AddValue(property, listener);
            return listener.Get(includeAnimations);
        }

        /// <summary>
        /// Gets a listener for an <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="includeAnimations">
        /// Whether to include property changes caused by animations.
        /// </param>
        /// <returns>The listener observable.</returns>
        public IObservable<AvaloniaPropertyChange<T>> Listen<T>(
            DirectPropertyBase<T> property,
            bool includeAnimations = true)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));

            _listeners ??= new AvaloniaPropertyValueStore<object>();

            if (_listeners.TryGetValue(property, out var existing))
            {
                return ((AvaloniaPropertyListener<T>)existing).Get(includeAnimations);
            }

            var listener = AvaloniaPropertyListener<T>.Create(this, property);
            _listeners.AddValue(property, listener);
            return listener.Get(includeAnimations);
        }

        /// <summary>
        /// Sets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        /// <param name="priority">The priority of the value.</param>
        public void SetValue(
            AvaloniaProperty property,
            object value,
            BindingPriority priority = BindingPriority.LocalValue)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));

            property.RouteSetValue(this, value, priority);
        }

        /// <summary>
        /// Sets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        /// <param name="priority">The priority of the value.</param>
        /// <returns>
        /// An <see cref="IDisposable"/> if setting the property can be undone, otherwise null.
        /// </returns>
        public IDisposable SetValue<T>(
            StyledPropertyBase<T> property,
            T value,
            BindingPriority priority = BindingPriority.LocalValue)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            LogPropertySet(property, value, priority);

            if (value is UnsetValueType)
            {
                if (priority == BindingPriority.LocalValue)
                {
                    Values.ClearLocalValue(property);
                }
                else
                {
                    throw new NotSupportedException(
                        "Cannot set property to Unset at non-local value priority.");
                }
            }
            else if (!(value is DoNothingType))
            {
                return Values.SetValue(property, value, priority);
            }

            return null;
        }

        /// <summary>
        /// Sets a <see cref="AvaloniaProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        public void SetValue<T>(DirectPropertyBase<T> property, T value)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            VerifyAccess();

            LogPropertySet(property, value, BindingPriority.LocalValue);
            SetDirectValueUnchecked(property, value);
        }

        /// <summary>
        /// Binds a <see cref="AvaloniaProperty"/> to an observable.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="source">The observable.</param>
        /// <param name="priority">The priority of the binding.</param>
        /// <returns>
        /// A disposable which can be used to terminate the binding.
        /// </returns>
        public IDisposable Bind<T>(
            StyledPropertyBase<T> property,
            IObservable<BindingValue<T>> source,
            BindingPriority priority = BindingPriority.LocalValue)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            source = source ?? throw new ArgumentNullException(nameof(source));
            VerifyAccess();

            return Values.AddBinding(property, source, priority);
        }

        /// <summary>
        /// Binds a <see cref="AvaloniaProperty"/> to an observable.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="source">The observable.</param>
        /// <returns>
        /// A disposable which can be used to terminate the binding.
        /// </returns>
        public IDisposable Bind<T>(
            DirectPropertyBase<T> property,
            IObservable<BindingValue<T>> source)
        {
            property = property ?? throw new ArgumentNullException(nameof(property));
            source = source ?? throw new ArgumentNullException(nameof(source));
            VerifyAccess();

            property = AvaloniaPropertyRegistry.Instance.GetRegisteredDirect(this, property);

            if (property.IsReadOnly)
            {
                throw new ArgumentException($"The property {property.Name} is readonly.");
            }

            Logger.TryGet(LogEventLevel.Verbose)?.Log(
                LogArea.Property,
                this,
                "Bound {Property} to {Binding} with priority LocalValue",
                property,
                GetDescription(source));

            _directBindings ??= new List<IDisposable>();

            return new DirectBindingSubscription<T>(this, property, source);
        }

        /// <summary>
        /// Coerces the specified <see cref="AvaloniaProperty"/>.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        public void CoerceValue<T>(StyledPropertyBase<T> property)
        {
            _values?.CoerceValue(property);
        }

        /// <inheritdoc/>
        void IAvaloniaObject.AddInheritanceChild(IAvaloniaObject child)
        {
            _inheritanceChildren ??= new List<IAvaloniaObject>();
            _inheritanceChildren.Add(child);
        }
        
        /// <inheritdoc/>
        void IAvaloniaObject.RemoveInheritanceChild(IAvaloniaObject child)
        {
            _inheritanceChildren?.Remove(child);
        }

        void IAvaloniaObject.InheritedPropertyChanged<T>(
            AvaloniaProperty<T> property,
            Optional<T> oldValue,
            Optional<T> newValue)
        {
            if (property.Inherits && (_values == null || !_values.IsSet(property)))
            {
                RaisePropertyChanged(property, oldValue, newValue, BindingPriority.LocalValue);
            }
        }

        /// <inheritdoc/>
        Delegate[] IAvaloniaObjectDebug.GetPropertyChangedSubscribers()
        {
            return _propertyChanged?.GetInvocationList();
        }

        void IValueSink.ValueChanged<T>(in AvaloniaPropertyChange<T> change)
        {
            var property = (StyledPropertyBase<T>)change.Property;
            var oldValue = change.OldValue.HasValue ? change.OldValue : GetInheritedOrDefault<T>(property);
            var newValue = change.NewValue.HasValue ? change.NewValue : change.NewValue.WithValue(GetInheritedOrDefault(property));

            LogIfError(property, newValue);

            if (!EqualityComparer<T>.Default.Equals(oldValue.Value, newValue.Value))
            {
                RaisePropertyChanged(new AvaloniaPropertyChange<T>(
                    this,
                    property,
                    oldValue,
                    newValue,
                    change.Priority,
                    change.IsActiveValueChange));

                Logger.TryGet(LogEventLevel.Verbose)?.Log(
                    LogArea.Property,
                    this,
                    "{Property} changed from {$Old} to {$Value} with priority {Priority}",
                    property,
                    oldValue,
                    newValue,
                    change.Priority);
            }
        }

        void IValueSink.Completed<T>(
            StyledPropertyBase<T> property,
            IPriorityValueEntry entry,
            Optional<T> oldValue) 
        {
            var change = new AvaloniaPropertyChange<T>(
                this,
                property,
                oldValue,
                default,
                BindingPriority.Unset);
            ((IValueSink)this).ValueChanged(change);
        }

        /// <summary>
        /// Called for each inherited property when the <see cref="InheritanceParent"/> changes.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="oldParent">The old inheritance parent.</param>
        internal void InheritanceParentChanged<T>(
            StyledPropertyBase<T> property,
            IAvaloniaObject oldParent)
        {
            var oldValue = oldParent switch
            {
                AvaloniaObject o => o.GetValueOrInheritedOrDefault(property),
                null => property.GetDefaultValue(GetType()),
                _ => oldParent.GetValue(property)
            };

            var newValue = GetInheritedOrDefault(property);

            if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                RaisePropertyChanged(property, oldValue, newValue);
            }
        }

        internal AvaloniaPropertyValue GetDiagnosticInternal(AvaloniaProperty property)
        {
            if (property.IsDirect)
            {
                return new AvaloniaPropertyValue(
                    property,
                    GetValue(property),
                    BindingPriority.Unset,
                    "Local Value");
            }
            else if (_values != null)
            {
                var result = _values.GetDiagnostic(property);

                if (result != null)
                {
                    return result;
                }
            }

            return new AvaloniaPropertyValue(
                property,
                GetValue(property),
                BindingPriority.Unset,
                "Unset");
        }

        /// <summary>
        /// Logs a binding error for a property.
        /// </summary>
        /// <param name="property">The property that the error occurred on.</param>
        /// <param name="e">The binding error.</param>
        protected internal virtual void LogBindingError(AvaloniaProperty property, Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning)?.Log(
                LogArea.Binding,
                this,
                "Error in binding to {Target}.{Property}: {Message}",
                this,
                property,
                e.Message);
        }

        /// <summary>
        /// Called to update the validation state for properties for which data validation is
        /// enabled.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The new binding value for the property.</param>
        protected virtual void UpdateDataValidation<T>(
            AvaloniaProperty<T> property,
            BindingValue<T> value)
        {
        }

        /// <summary>
        /// Called when a avalonia property changes on the object.
        /// </summary>
        /// <param name="change">The property change details.</param>
        protected virtual void OnPropertyChanged<T>(in AvaloniaPropertyChange<T> change)
        {
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="property">The property that has changed.</param>
        /// <param name="oldValue">The old property value.</param>
        /// <param name="newValue">The new property value.</param>
        /// <param name="priority">The priority of the binding that produced the value.</param>
        protected internal void RaisePropertyChanged<T>(
            AvaloniaProperty<T> property,
            Optional<T> oldValue,
            BindingValue<T> newValue,
            BindingPriority priority = BindingPriority.LocalValue)
        {
            RaisePropertyChanged(new AvaloniaPropertyChange<T>(
                this,
                property,
                oldValue,
                newValue,
                priority));
        }

        /// <summary>
        /// Sets the backing field for a direct avalonia property, raising the 
        /// <see cref="PropertyChanged"/> event if the value has changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// True if the value changed, otherwise false.
        /// </returns>
        protected bool SetAndRaise<T>(AvaloniaProperty<T> property, ref T field, T value)
        {
            VerifyAccess();

            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            var old = field;
            field = value;
            RaisePropertyChanged(property, old, value);
            return true;
        }

        private T GetInheritedOrDefault<T>(StyledPropertyBase<T> property)
        {
            if (property.Inherits && InheritanceParent is AvaloniaObject o)
            {
                return o.GetValueOrInheritedOrDefault(property);
            }

            return property.GetDefaultValue(GetType());
        }

        private T GetValueOrInheritedOrDefault<T>(
            StyledPropertyBase<T> property,
            bool includeAnimations = true)
        {
            var o = this;
            var inherits = property.Inherits;
            var value = default(T);

            while (o != null)
            {
                var values = o._values;

                if (values?.TryGetValue(property, includeAnimations, out value) == true)
                {
                    return value;
                }

                if (!inherits)
                {
                    break;
                }

                o = o.InheritanceParent as AvaloniaObject;
            }

            return property.GetDefaultValue(GetType());
        }

        protected internal void RaisePropertyChanged<T>(in AvaloniaPropertyChange<T> change)
        {
            VerifyAccess();

            change.Property.Notifying?.Invoke(this, true);

            try
            {
                AvaloniaPropertyChangedEventArgs<T> e = null;
                var hasChanged = change.Property.HasChangedSubscriptions;

                if (hasChanged || _propertyChanged != null)
                {
                    e = new AvaloniaPropertyChangedEventArgs<T>(
                        this,
                        change.Property,
                        change.OldValue,
                        change.NewValue,
                        change.Priority);
                }

                OnPropertyChanged(change);

                if (hasChanged)
                {
                    change.Property.NotifyChanged(e);
                }

                if (_listeners != null && _listeners.TryGetValue(change.Property, out var listener))
                {
                    ((AvaloniaPropertyListener<T>)listener).Signal(change);
                }

                _propertyChanged?.Invoke(this, e);

                if (_inpcChanged != null)
                {
                    var inpce = new PropertyChangedEventArgs(change.Property.Name);
                    _inpcChanged(this, inpce);
                }

                if (change.Property.Inherits && _inheritanceChildren != null)
                {
                    foreach (var child in _inheritanceChildren)
                    {
                        child.InheritedPropertyChanged(
                            change.Property,
                            change.OldValue,
                            change.NewValue.ToOptional());
                    }
                }
            }
            finally
            {
                change.Property.Notifying?.Invoke(this, false);
            }
        }

        /// <summary>
        /// Sets the value of a direct property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        private void SetDirectValueUnchecked<T>(DirectPropertyBase<T> property, T value)
        {
            var p = AvaloniaPropertyRegistry.Instance.GetRegisteredDirect(this, property);

            if (value is UnsetValueType)
            {
                p.InvokeSetter(this, p.GetUnsetValue(GetType()));
            }
            else if (!(value is DoNothingType))
            {
                p.InvokeSetter(this, value);
            }
        }

        /// <summary>
        /// Sets the value of a direct property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        private void SetDirectValueUnchecked<T>(DirectPropertyBase<T> property, BindingValue<T> value)
        {
            var p = AvaloniaPropertyRegistry.Instance.FindRegisteredDirect(this, property);

            if (p == null)
            {
                throw new ArgumentException($"Property '{property.Name} not registered on '{this.GetType()}");
            }

            LogIfError(property, value);

            switch (value.Type)
            {
                case BindingValueType.UnsetValue:
                case BindingValueType.BindingError:
                    var fallback = value.HasValue ? value : value.WithValue(property.GetUnsetValue(GetType()));
                    property.InvokeSetter(this, fallback);
                    break;
                case BindingValueType.DataValidationError:
                    property.InvokeSetter(this, value);
                    break;
                case BindingValueType.Value:
                case BindingValueType.BindingErrorWithFallback:
                case BindingValueType.DataValidationErrorWithFallback:
                    property.InvokeSetter(this, value);
                    break;
            }

            if (p.IsDataValidationEnabled)
            {
                UpdateDataValidation(property, value);
            }
        }

        /// <summary>
        /// Gets a description of an observable that van be used in logs.
        /// </summary>
        /// <param name="o">The observable.</param>
        /// <returns>The description.</returns>
        private string GetDescription(object o)
        {
            var description = o as IDescription;
            return description?.Description ?? o.ToString();
        }

        /// <summary>
        /// Logs a mesage if the notification represents a binding error.
        /// </summary>
        /// <param name="property">The property being bound.</param>
        /// <param name="value">The binding notification.</param>
        private void LogIfError<T>(AvaloniaProperty property, BindingValue<T> value)
        {
            if (value.HasError)
            {
                if (value.Error is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        LogBindingError(property, inner);
                    }
                }
                else
                {
                    LogBindingError(property, value.Error);
                }
            }
        }

        /// <summary>
        /// Logs a property set message.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The new value.</param>
        /// <param name="priority">The priority.</param>
        private void LogPropertySet<T>(AvaloniaProperty<T> property, T value, BindingPriority priority)
        {
            Logger.TryGet(LogEventLevel.Verbose)?.Log(
                LogArea.Property,
                this,
                "Set {Property} to {$Value} with priority {Priority}",
                property,
                value,
                priority);
        }

        private class DirectBindingSubscription<T> : IObserver<BindingValue<T>>, IDisposable
        {
            private readonly AvaloniaObject _owner;
            private readonly DirectPropertyBase<T> _property;
            private readonly IDisposable _subscription;

            public DirectBindingSubscription(
                AvaloniaObject owner,
                DirectPropertyBase<T> property,
                IObservable<BindingValue<T>> source)
            {
                _owner = owner;
                _property = property;
                _owner._directBindings.Add(this);
                _subscription = source.Subscribe(this);
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _owner._directBindings.Remove(this);
            }

            public void OnCompleted() => Dispose();
            public void OnError(Exception error) => Dispose();
            public void OnNext(BindingValue<T> value)
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    _owner.SetDirectValueUnchecked(_property, value);
                }
                else
                {
                    // To avoid allocating closure in the outer scope we need to capture variables
                    // locally. This allows us to skip most of the allocations when on UI thread.
                    var instance = _owner;
                    var property = _property;
                    var newValue = value;

                    Dispatcher.UIThread.Post(() => instance.SetDirectValueUnchecked(property, newValue));
                }
            }
        }
    }
}
