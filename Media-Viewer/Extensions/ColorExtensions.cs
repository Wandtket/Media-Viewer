using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudio.Extensions
{

    public static class ColorExtensions
    {
        public static Windows.UI.Color IsThemeAware(this Windows.UI.Color color)
        {
            return Windows.UI.Color.FromArgb(110, color.R, color.G, color.B);
        }
    }


}
