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
        private bool isCurrentTrack; // New property to track if this is the loaded track

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
            DisplayTitle = !string.IsNullOrEmpty(properties?.Title)
                ? properties.Title
                : file.DisplayName;

            DisplayArtist = !string.IsNullOrEmpty(properties?.Author)
                ? properties.Author
                : "Unknown";

            // Duration will be set when audio is loaded
            DisplayDuration = displayDuration ?? "00:00";
        }

        public void RefreshDisplay()
        {
            // Use the property setters (uppercase) instead of field assignments
            // This ensures PropertyChanged notifications are raised
            DisplayTitle = !string.IsNullOrEmpty(properties?.Title)
                ? properties.Title
                : file.DisplayName;

            DisplayArtist = !string.IsNullOrEmpty(properties?.Author)
                ? properties.Author
                : "Unknown";
        }
    }
}