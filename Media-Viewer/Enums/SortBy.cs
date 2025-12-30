using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaViewer.Enums
{
    public enum SortBy
    {
        Date_Modified,
        Date_Created,
        Name,
        Size,
        Rating
    }

    public class SortBys
    {
        public string Key { get; set; }

        public SortBy Value { get; set; }

        public static ObservableCollection<SortBys> List { get; } = new()
        {
            new SortBys { Key = "Date Modified", Value = SortBy.Date_Modified },
            new SortBys { Key = "Date Created", Value = SortBy.Date_Created },
            new SortBys { Key = "Name", Value = SortBy.Name },
            new SortBys { Key = "Size", Value = SortBy.Size },
            new SortBys { Key = "Rating", Value = SortBy.Rating },
        };
    }



}
