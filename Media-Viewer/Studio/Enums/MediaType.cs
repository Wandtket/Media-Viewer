using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudio.Studio.Enums
{
    public enum MediaType
    {
        Audio,
        Video,
        Image,
        Gif,
        Unknown
    }

    public class MediaTypes
    {
        public List<MediaType> List()
        {
            return Enum.GetValues(typeof(MediaType)).Cast<MediaType>().ToList();
        }

    }

}