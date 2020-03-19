// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
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

        private class Class1 : AvaloniaObject
        {
            public static readonly StyledProperty<string> FooProperty =
                AvaloniaProperty.Register<Class1, string>("Foo", "foodefault");

            public static readonly StyledProperty<string> BarProperty =
                AvaloniaProperty.Register<Class1, string>("Bar", "bardefault");
        }
    }
}
