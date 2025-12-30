using System;
using System.Collections.Generic;
using System.Text;

namespace MediaStudio.Extensions
{
    public static class TimeSpanExtensions
    {

        /// <summary>
        /// Formats a timestamp for use in filenames.
        /// </summary>
        public static string FormatTimestampForFilename(this TimeSpan position)
        {
            int hours = (int)position.TotalHours;
            return $"{hours:00}-{position.Minutes:00}-{position.Seconds:00}-{position.Milliseconds:000}";
        }

    }
}
