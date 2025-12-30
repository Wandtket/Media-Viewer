using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudio.Studio.Enums
{
    public enum SortDirection
    {
        Descending,
        Ascending    
    }

    public class SortDirections
    {
        public static List<SortDirection> List = Enum.GetValues(typeof(SortDirection)).Cast<SortDirection>().ToList();
    }

}
