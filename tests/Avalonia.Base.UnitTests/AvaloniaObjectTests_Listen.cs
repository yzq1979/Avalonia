// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Data;
using Xunit;

namespace Avalonia.Base.UnitTests
{
    public class AvaloniaObjectTests_Listen
    {
        [Fact]
        public void Listen_Returns_Initial_Value_Immediately()
        {
            var target = new Class1();
            var raised = 0;

            target.Listen(Class1.FooProperty).Subscribe(x =>
            {
                Assert.Equal("foodefault", x.NewValue.Value);
                Assert.False(x.OldValue.HasValue);
                Assert.Equal(BindingPriority.Unset, x.Priority);
                Assert.True(x.IsActiveValueChange);
                Assert.False(x.IsOutdated);
                ++raised;
            });

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Listener_Fires_On_Property_Change()
        {
            var target = new Class1();
            var raised = 0;

            target.Listen(Class1.FooProperty).Skip(1).Subscribe(x =>
            {
                Assert.Equal("newvalue", x.NewValue.Value);
                Assert.Equal("foodefault", x.OldValue.Value);
                Assert.Equal(BindingPriority.LocalValue, x.Priority);
                Assert.True(x.IsActiveValueChange);
                Assert.False(x.IsOutdated);
                ++raised;
            });

            target.SetValue(Class1.FooProperty, "newvalue");

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Listener_Signals_Outdated_Change()
        {
            var target = new Class1();
            var raised = 0;

            target.Listen(Class1.FooProperty).Skip(1).Subscribe(x =>
            {
                // In the handler for the change to "value1", set the value to "value2".
                target.SetValue(Class1.FooProperty, "value2");
            });

            target.Listen(Class1.FooProperty).Skip(1).Subscribe(x =>
            {
                // This handler was added after the handler which changes the value.
                // It should receive both changes in order, with the first one marked
                // outdated because by the time this handler receives the notification,
                // the value has already been set to "value2".
                if (raised == 0)
                {
                    Assert.Equal("value1", x.NewValue.Value);
                    Assert.True(x.IsOutdated);
                }
                else if (raised == 1)
                {
                    Assert.Equal("value2", x.NewValue.Value);
                    Assert.False(x.IsOutdated);
                }

                ++raised;
            });


            target.SetValue(Class1.FooProperty, "value1");

            Assert.Equal(2, raised);
        }

        [Fact]
        public void Listener_Returns_Property_Change_Only_For_Correct_Property()
        {
            var target = new Class1();
            var result = new List<string>();

            target.Listen(Class1.FooProperty).Subscribe(x => result.Add(x.NewValue.Value));
            target.SetValue(Class1.BarProperty, "newvalue");

            Assert.Equal(new[] { "foodefault" }, result);
        }

        [Fact]
        public void Listener_Dispose_Stops_Property_Changes()
        {
            Class1 target = new Class1();
            bool raised = false;

            target.Listen(Class1.FooProperty)
                  .Subscribe(x => raised = true)
                  .Dispose();
            raised = false;
            target.SetValue(Class1.FooProperty, "newvalue");

            Assert.False(raised);
        }

        public class NoAnimation
        {
            [Fact]
            public void Listen_Returns_Default_Value_Immediately_When_Only_Animated_Value_Present()
            {
                var target = new Class1();
                var raised = 0;

                target.SetValue(Class1.FooProperty, "a1", BindingPriority.Animation);

                target.Listen(Class1.FooProperty, false).Subscribe(x =>
                {
                    Assert.Equal("foodefault", x.NewValue.Value);
                    Assert.False(x.OldValue.HasValue);
                    Assert.Equal(BindingPriority.Unset, x.Priority);
                    Assert.True(x.IsActiveValueChange);
                    Assert.False(x.IsOutdated);
                    ++raised;
                });

                Assert.Equal(1, raised);
            }

            [Fact]
            public void Listener_Fires_Only_On_Non_Animated_Property_Changes()
            {
                var target = new Class1();
                var changes = new List<AvaloniaPropertyChange<string>>();

                target.Listen(Class1.FooProperty, false).Skip(1).Subscribe(x => changes.Add(x));

                target.SetValue(Class1.FooProperty, "a1", BindingPriority.Animation);
                target.SetValue(Class1.FooProperty, "l1");
                target.SetValue(Class1.FooProperty, "l2");
                target.SetValue(Class1.FooProperty, "a2", BindingPriority.Animation);
                target.SetValue(Class1.FooProperty, "l3");

                Assert.Equal(new[] { "l1", "l2", "l3" }, changes.Select(x => x.NewValue.Value).ToList());
                Assert.True(changes.All(x => !x.IsActiveValueChange));
            }

            [Fact]
            public void Listener_Fires_Only_On_Non_Animated_Property_Changes_With_Binding_Added_Midway()
            {
                var target = new Class1();
                var changes = new List<AvaloniaPropertyChange<string>>();
                var style = new BehaviorSubject<BindingValue<string>>("s1");

                target.Listen(Class1.FooProperty, false).Skip(1).Subscribe(x => changes.Add(x));

                target.SetValue(Class1.FooProperty, "l1");
                target.Bind(Class1.FooProperty, style, BindingPriority.Style);
                target.SetValue(Class1.FooProperty, "a1", BindingPriority.Animation);
                target.SetValue(Class1.FooProperty, "l2");
                target.SetValue(Class1.FooProperty, "a2", BindingPriority.Animation);
                target.SetValue(Class1.FooProperty, "l3");

                Assert.Equal(new[] { "l1", "l2", "l3" }, changes.Select(x => x.NewValue.Value).ToList());
                Assert.Equal(new[] { true, false, false }, changes.Select(x => x.IsActiveValueChange).ToList());
            }

            [Fact]
            public void Listener_Fires_Only_On_Non_Animated_Binding_Property_Changes()
            {
                var target = new Class1();
                var allChanges = new List<AvaloniaPropertyChange<string>>();
                var nonAnimatedChanges = new List<AvaloniaPropertyChange<string>>();
                var style = new Subject<BindingValue<string>>();
                var animation = new Subject<BindingValue<string>>();
                var templatedParent = new Subject<BindingValue<string>>();

                target.Bind(Class1.FooProperty, style, BindingPriority.Style);
                target.Bind(Class1.FooProperty, animation, BindingPriority.Animation);
                target.Bind(Class1.FooProperty, templatedParent, BindingPriority.TemplatedParent);

                target.Listen(Class1.FooProperty, true).Subscribe(x => allChanges.Add(x));
                target.Listen(Class1.FooProperty, false).Subscribe(x => nonAnimatedChanges.Add(x));

                style.OnNext("style1");
                templatedParent.OnNext("tp1");
                animation.OnNext("a1");
                templatedParent.OnNext("tp2");
                templatedParent.OnCompleted();
                animation.OnNext("a2");
                style.OnNext("style2");
                style.OnCompleted();
                animation.OnCompleted();

                Assert.Equal(
                    new[] { "foodefault", "style1", "tp1", "tp2", "style1", "style2", "foodefault" },
                    nonAnimatedChanges.Select(x => x.NewValue.Value).ToList());
                Assert.Equal(
                    new[] { true, true, true, false, false, false, false },
                    nonAnimatedChanges.Select(x => x.IsActiveValueChange).ToList());
                Assert.Equal(
                    new[] { "foodefault", "style1", "tp1", "a1", "a2", "foodefault" },
                    allChanges.Select(x => x.NewValue.Value).ToList());
                Assert.True(allChanges.All(x => x.IsActiveValueChange));
            }
        }

        private class Class1 : AvaloniaObject
        {
            public static readonly StyledProperty<string> FooProperty =
                AvaloniaProperty.Register<Class1, string>("Foo", "foodefault");

            public static readonly StyledProperty<string> BarProperty =
                AvaloniaProperty.Register<Class1, string>("Bar", "bardefault");
        }
    }
}
