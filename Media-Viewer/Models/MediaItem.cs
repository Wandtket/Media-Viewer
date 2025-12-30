using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaViewer.Models
{

    public partial class MediaItem : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string index;

        [ObservableProperty]
        private string fileLocation;

        [ObservableProperty]
        private string fileType;

    }
}
