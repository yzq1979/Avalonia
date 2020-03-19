// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using Avalonia.Data;

#nullable enable

namespace Avalonia
{
    /// <summary>
    /// Provides information about an <see cref="AvaloniaProperty"/> change.
    /// </summary>
    public readonly struct AvaloniaPropertyChange<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaPropertyChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sender">The object on which the property changed.</param>
        /// <param name="property">The property that changed.</param>
        /// <param name="oldValue">The old value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        /// <param name="priority">The priority of the binding that produced the value.</param>
        /// <param name="isActiveValueChange">
        /// Whether the change represents a change to the active value.
        /// </param>
        /// <param name="isOutdated">Whether the value is outdated.</param>
        public AvaloniaPropertyChange(
            IAvaloniaObject sender,
            AvaloniaProperty<T> property,
            Optional<T> oldValue,
            BindingValue<T> newValue,
            BindingPriority priority,
            bool isActiveValueChange = true,
            bool isOutdated = false)
        {
            Sender = sender;
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
            Priority = priority;
            IsActiveValueChange = isActiveValueChange;
            IsOutdated = isOutdated;
        }

        /// <summary>
        /// Gets the property that changed.
        /// </summary>
        public AvaloniaProperty<T> Property { get; }

        /// <summary>
        /// Gets the object on which the property changed.
        /// </summary>
        public IAvaloniaObject Sender { get; }

        /// <summary>
        /// Gets the old value of the property.
        /// </summary>
        public Optional<T> OldValue { get; }

        /// <summary>
        /// Gets the new value of the property.
        /// </summary>
        public BindingValue<T> NewValue { get; }

        /// <summary>
        /// Gets the priority at which the change occurred.
        /// </summary>
        public BindingPriority Priority { get; }

        /// <summary>
        /// Gets a value indicating whether the change represents a change to the active value of
        /// the property.
        /// </summary>
        /// <remarks>
        /// If the Listen call requested to not include animation changes then <see cref="NewValue"/>
        /// may not represent a change to the active value of the property on the object.
        /// </remarks>
        public bool IsActiveValueChange { get; }

        /// <summary>
        /// Gets a value indicating whether the value of the property on the object has already
        /// changed since this change began notifying.
        /// </summary>
        public bool IsOutdated { get; }

        /// <summary>
        /// Makes a copy of the structure with <see cref="IsOutdated"/> set to true.
        /// </summary>
        public AvaloniaPropertyChange<T> MakeOutdated()
        {
            return new AvaloniaPropertyChange<T>(
                Sender,
                Property,
                OldValue,
                NewValue,
                Priority,
                IsActiveValueChange,
                true);
        }
    }
}
