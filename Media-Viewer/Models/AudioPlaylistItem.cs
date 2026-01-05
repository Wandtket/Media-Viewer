using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Storage;

namespace MediaViewer.Models
{
    /// <summary>
    /// Represents an audio file item in the playlist
    /// </summary>
    public partial class AudioPlaylistItem : ObservableObject
    {
        [ObservableProperty]
        private StorageFile file;

        [ObservableProperty]
        private MediaProperties properties;

        [ObservableProperty]
        private bool isPlaying;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private int trackNumber;

        [ObservableProperty]
        private string displayTitle;

        [ObservableProperty]
        private string displayArtist;

        [ObservableProperty]
        private string displayDuration;

        public AudioPlaylistItem(StorageFile file, int trackNumber = 0)
        {
            this.file = file;
            this.trackNumber = trackNumber;
            this.properties = new MediaProperties(file);
            UpdateDisplayInfo();
        }

        private void UpdateDisplayInfo()
        {
            displayTitle = !string.IsNullOrEmpty(properties?.Title) 
                ? properties.Title 
                : file.DisplayName;

            displayArtist = !string.IsNullOrEmpty(properties?.Author) 
                ? properties.Author 
                : "Unknown";

            // Duration will be set when audio is loaded
            displayDuration = "00:00";
        }

        public void RefreshDisplay()
        {
            UpdateDisplayInfo();
        }
    }
}
