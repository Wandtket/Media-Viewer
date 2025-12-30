using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MediaViewer.Models
{

    public class ShareData
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public IRandomAccessStream Stream { get; set; }
        public StorageFile StorageFile { get; set; }
        public bool IsTemporary { get; set; }
    }

}
