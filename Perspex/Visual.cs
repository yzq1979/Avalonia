﻿// -----------------------------------------------------------------------
// <copyright file="Visual.cs" company="Steven Kirk">
// Copyright 2014 MIT Licence. See licence.md for more information.
// </copyright>
// -----------------------------------------------------------------------

namespace Perspex
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using Perspex.Controls;
    using Perspex.Media;

    public abstract class Visual : PerspexObject, IVisual, ILogical
    {
        public static readonly ReadOnlyPerspexProperty<Control> ParentProperty =
            new ReadOnlyPerspexProperty<Control>(ParentPropertyRW);

        internal static readonly PerspexProperty<Control> ParentPropertyRW =
            PerspexProperty.Register<Control, Control>("Parent");

        private ILogical logicalParent;

        private IVisual visualParent;

        public Rect Bounds
        {
            get;
            protected set;
        }

        public Control Parent
        {
            get { return this.GetValue(ParentPropertyRW); }
            protected set { this.SetValue(ParentPropertyRW, value); }
        }

        ILogical ILogical.LogicalParent
        {
            get 
            { 
                return this.logicalParent; 
            }
            
            set 
            { 
                this.logicalParent = value;
                this.Parent = value as Control;
            }
        }

        IEnumerable<ILogical> ILogical.LogicalChildren
        {
            get { return new ILogical[0]; }
        }

        IEnumerable<IVisual> IVisual.VisualChildren
        {
            get { return Enumerable.Empty<Visual>(); }
        }

        IVisual IVisual.VisualParent
        {
            get 
            { 
                return this.visualParent;
            }

            set
            {
                if (this.visualParent != value)
                {
                    this.visualParent = value;
                    this.InheritanceParent = (PerspexObject)value;
                }
            }
        }

        public virtual void Render(IDrawingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null);
        }
    }
}
