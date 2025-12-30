using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MediaStudio.Studio.Models
{

    /// <summary>
    /// Represents the result of extracting a frame from media.
    /// </summary>
    public class FrameExtractionResult
    {
        public StorageFile File { get; set; }
        public InMemoryRandomAccessStream Stream { get; set; }
        public string FileName { get; set; }
        public string Description { get; set; }
        public bool IsTemporary { get; set; }
    }

}
