using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudio.Studio.Enums
{
    public enum Backdrop
    {
        System,
        Mica,
        Accrylic,
    }

    public class Backdrops
    {
        public static List<Backdrop> List = Enum.GetValues(typeof(Backdrop)).Cast<Backdrop>().ToList();
    }

}
