using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudio.Controls
{
    public class VisualCanvas : Canvas
    {

        public InputCursor InputCursor
        {
            get => ProtectedCursor;
            set => ProtectedCursor = value;
        }

    }
}
