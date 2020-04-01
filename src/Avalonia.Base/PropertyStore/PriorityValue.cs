using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Data;

#nullable enable

namespace Avalonia.PropertyStore
{
    /// <summary>
    /// Stores a set of prioritized values and bindings in a <see cref="ValueStore"/>.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <remarks>
    /// When more than a single value or binding is applied to a property in an
    /// <see cref="AvaloniaObject"/>, the entry in the <see cref="ValueStore"/> is converted into
    /// a <see cref="PriorityValue{T}"/>. This class holds any number of
    /// <see cref="IPriorityValueEntry{T}"/> entries (sorted first by priority and then in the order
    /// they were added) plus a local value.
    /// </remarks>
    internal class PriorityValue<T> : IValue<T>, IValueSink
    {
        private readonly IAvaloniaObject _owner;
        private readonly IValueSink _sink;
        private readonly List<IPriorityValueEntry<T>> _entries = new List<IPriorityValueEntry<T>>();
        private readonly Func<IAvaloniaObject, T, T>? _coerceValue;
        private Optional<T> _localValue;
        private Optional<T> _nonAnimatedValue;
        private Optional<T> _value;

        public PriorityValue(
            IAvaloniaObject owner,
            StyledPropertyBase<T> property,
            IValueSink sink)
        {
            _owner = owner;
            Property = property;
            _sink = sink;

            if (property.HasCoercion)
            {
                var metadata = property.GetMetadata(owner.GetType());
                _coerceValue = metadata.CoerceValue;
            }
        }

        public PriorityValue(
            IAvaloniaObject owner,
            StyledPropertyBase<T> property,
            IValueSink sink,
            IPriorityValueEntry<T> existing)
            : this(owner, property, sink)
        {
            existing.Reparent(this);
            _entries.Add(existing);

            var v = existing.GetValue(true);
            
            if (v.HasValue)
            {
                _value = v;
                ValuePriority = existing.Priority;
            }

            v = existing.GetValue(false);

            if (v.HasValue)
            {
                _nonAnimatedValue = existing.GetValue(false);
            }
        }

        public PriorityValue(
            IAvaloniaObject owner,
            StyledPropertyBase<T> property,
            IValueSink sink,
            LocalValueEntry<T> existing)
            : this(owner, property, sink)
        {
            _value = _nonAnimatedValue = _localValue = existing.GetValue(false);
            ValuePriority = BindingPriority.LocalValue;
        }

        public StyledPropertyBase<T> Property { get; }
        public BindingPriority ValuePriority { get; private set; }
        public IReadOnlyList<IPriorityValueEntry<T>> Entries => _entries;
        Optional<object> IValue.GetValue() => _value.ToObject();

        public void ClearLocalValue() => UpdateEffectiveValue();

        public Optional<T> GetValue(bool includeAnimations)
        {
            return includeAnimations ? _value : _nonAnimatedValue;
        }

        public IDisposable? SetValue(T value, BindingPriority priority)
        {
            IDisposable? result = null;

            if (priority == BindingPriority.LocalValue)
            {
                _localValue = value;
            }
            else
            {
                var insert = FindInsertPoint(priority);
                var entry = new ConstantValueEntry<T>(Property, value, priority, this);
                _entries.Insert(insert, entry);
                result = entry;
            }

            UpdateEffectiveValue();
            return result;
        }

        public BindingEntry<T> AddBinding(IObservable<BindingValue<T>> source, BindingPriority priority)
        {
            var binding = new BindingEntry<T>(_owner, Property, source, priority, this);
            var insert = FindInsertPoint(binding.Priority);
            _entries.Insert(insert, binding);
            return binding;
        }

        public void CoerceValue() => UpdateEffectiveValue();

        void IValueSink.ValueChanged<TValue>(in AvaloniaPropertyChange<TValue> change)
        {
            if (change.Priority == BindingPriority.LocalValue)
            {
                _localValue = default;
            }

            UpdateEffectiveValue();
        }

        void IValueSink.Completed<TValue>(
            StyledPropertyBase<TValue> property,
            IPriorityValueEntry entry,
            Optional<TValue> oldValue)
        {
            _entries.Remove((IPriorityValueEntry<T>)entry);
            UpdateEffectiveValue();
        }

        private int FindInsertPoint(BindingPriority priority)
        {
            var result = _entries.Count;

            for (var i = 0; i < _entries.Count; ++i)
            {
                if (_entries[i].Priority < priority)
                {
                    result = i;
                    break;
                }
            }

            return result;
        }

        private void UpdateEffectiveValue()
        {
            var reachedLocalValues = false;
            var value = default(Optional<T>);
            var nonAnimatedValue = default(Optional<T>);
            var nonAnimatedValuePriority = BindingPriority.Unset;

            bool LoadLocalValue()
            {
                if (_localValue.HasValue)
                {
                    if (!value.HasValue)
                    {
                        value = _localValue;
                        ValuePriority = BindingPriority.LocalValue;
                    }

                    if (!nonAnimatedValue.HasValue)
                    {
                        nonAnimatedValue = _localValue;
                        nonAnimatedValuePriority = BindingPriority.LocalValue;
                    }
                }

                return _localValue.HasValue;
            }

            for (var i = _entries.Count - 1; i >= 0; --i)
            {
                var entry = _entries[i];

                if (!reachedLocalValues && entry.Priority >= BindingPriority.LocalValue)
                {
                    reachedLocalValues = true;

                    if (LoadLocalValue())
                    {
                        break;
                    }
                }

                var entryValue = entry.GetValue(true);

                if (entryValue.HasValue)
                {
                    if (!value.HasValue)
                    {
                        value = entry.GetValue(true);
                        ValuePriority = entry.Priority;
                    }

                    if (entry.Priority > BindingPriority.Animation)
                    {
                        nonAnimatedValue = entryValue;
                        nonAnimatedValuePriority = entry.Priority;
                    }
                }

                if (value.HasValue && nonAnimatedValue.HasValue)
                {
                    break;
                }
            }

            LoadLocalValue();

            if (value.HasValue && _coerceValue != null)
            {
                value = _coerceValue(_owner, value.Value);
            }

            if (nonAnimatedValue.HasValue && _coerceValue != null)
            {
                nonAnimatedValue = _coerceValue(_owner, nonAnimatedValue.Value);
            }

            if (value != _value)
            {
                var old = _value;
                _value = value;

                if (ValuePriority > BindingPriority.Animation)
                {
                    _nonAnimatedValue = value;
                }

                _sink.ValueChanged(new AvaloniaPropertyChange<T>(
                    _owner,
                    Property,
                    old,
                    value,
                    ValuePriority));
            }

            if (nonAnimatedValue != _nonAnimatedValue && ValuePriority < BindingPriority.LocalValue)
            {
                var old = _nonAnimatedValue;
                _nonAnimatedValue = nonAnimatedValue;

                if (_nonAnimatedValue != _value)
                {
                    _sink.ValueChanged(new AvaloniaPropertyChange<T>(
                        _owner,
                        Property,
                        old,
                        nonAnimatedValue,
                        nonAnimatedValuePriority,
                        false));
                }
            }
        }
    }
}
