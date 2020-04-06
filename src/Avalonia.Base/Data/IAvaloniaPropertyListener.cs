using System;
using System.Collections.Generic;
using System.Text;

namespace Avalonia.Data
{
    public interface IAvaloniaPropertyListener
    {
        void PropertyChanged<T>(in AvaloniaPropertyChange<T> change);
    }
}
