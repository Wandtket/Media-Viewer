using System;
using System.Collections.Generic;
using System.Text;

namespace MediaStudio.Studio.Models
{
    public class PlaybackState
    {
        public TimeSpan Position { get; set; }
        public double Volume { get; set; } = 1.0;
        public TimeSpan? MarkIn { get; set; }
        public TimeSpan? MarkOut { get; set; }
    }

}
