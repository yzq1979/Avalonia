﻿// -----------------------------------------------------------------------
// <copyright file="ContentControl.cs" company="Steven Kirk">
// Copyright 2014 MIT Licence. See licence.md for more information.
// </copyright>
// -----------------------------------------------------------------------

namespace Perspex.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class ContentControl : TemplatedControl
    {
        public static readonly PerspexProperty<object> ContentProperty =
            PerspexProperty.Register<ContentControl, object>("Content");

        public ContentControl()
        {
            this.GetObservable(ContentProperty).Subscribe(x =>
            {
                IVisual visual = x as IVisual;
                ILogical logical = x as ILogical;

                if (visual != null)
                {
                    visual.VisualParent = this;
                }

                if (logical != null)
                {
                    logical.LogicalParent = this;
                }
            });
        }

        public object Content
        {
            get { return this.GetValue(ContentProperty); }
            set { this.SetValue(ContentProperty, value); }
        }

        protected override Size ArrangeContent(Size finalSize)
        {
            Control child = ((IVisual)this).VisualChildren.SingleOrDefault() as Control;

            if (child != null)
            {
                child.Arrange(new Rect(finalSize));
                return child.Bounds.Size;
            }
            else
            {
                return new Size();
            }
        }

        protected override Size MeasureContent(Size availableSize)
        {
            Control child = ((IVisual)this).VisualChildren.SingleOrDefault() as Control;

            if (child != null)
            {
                child.Measure(availableSize);
                return child.DesiredSize.Value;
            }
            else
            {
                return new Size();
            }
        }
    }
}
