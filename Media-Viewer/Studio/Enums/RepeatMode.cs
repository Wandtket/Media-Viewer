using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MediaStudio.Studio.Enums
{
    public enum RepeatMode
    {
        Off = 0,
        RepeatOne = 1,
        RepeatAll = 2
    }


    public class RepeatModes
    {
        public string Key { get; set; }

        public RepeatMode Value { get; set; }

        public static ObservableCollection<RepeatModes> List { get; } = new()
        {
            new RepeatModes { Key = "Off", Value = RepeatMode.Off },
            new RepeatModes { Key = "Repeat One", Value = RepeatMode.RepeatOne },
            new RepeatModes { Key = "Repeat All", Value =  RepeatMode.RepeatAll }
        };
    }
}
