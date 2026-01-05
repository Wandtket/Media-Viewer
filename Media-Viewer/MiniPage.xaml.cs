using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using MediaViewer.Models;
using MediaViewer.Pages;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace MediaViewer
{
    public sealed partial class MiniPage : Page
    {

        private bool isNarrowView = false;
        private bool isTransportNarrow = false;
        private MediaProperties currentMediaProperties;
        private bool isUpdatingUI = false;

        // Playlist management
        private ObservableCollection<AudioPlaylistItem> playlist = new ObservableCollection<AudioPlaylistItem>();
        private AudioPlaylistItem currentItem;

        // Playback
        private MediaPlayer Player;
        private DispatcherTimer ProgressTimer;
        private bool isScrubbing = false;
        private bool isLooping = false;

        private bool isDraggingSlider = false;
        private Thumb? transportSliderThumb;

        // Shuffle
        private bool isShuffleEnabled = false;
        private List<int> shuffleIndices = new List<int>();
        private int currentShuffleIndex = 0;

        // Volume
        private double lastVolume = 1.0;
        private bool isMuted = false;


        public MiniPage(StorageFile? File = null)
        {
            InitializeComponent();
            Initialize(File);
        }


        private async void Initialize(StorageFile? File = null)
        {
            while (App.Current.ActiveWindow == null) { await Task.Delay(50); }
            while (App.Current.ActiveWindow.Content == null) { await Task.Delay(50); }
            while (App.Current.ActiveWindow.Content.XamlRoot == null) { await Task.Delay(50); }

            App.Current.ActiveWindow.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Standard;

            Player = new MediaPlayer();
            Player.MediaOpened += Player_MediaOpened;
            Player.MediaEnded += Player_MediaEnded;
            Player.CurrentStateChanged += Player_CurrentStateChanged;

            // Set up progress timer
            ProgressTimer = new DispatcherTimer();
            ProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            ProgressTimer.Tick += ProgressTimer_Tick;

            ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ProgressSlider_PointerPressed), true);
            ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);


            // Set initial volume (will be applied when flyout opens)
            Player.Volume = 1.0;

            if (File != null)
            {
                await LoadAudioFile(File);
            }
        }



        #region File Loading

        private async Task LoadAudioFile(StorageFile file)
        {
            try
            {
                // Get sort order from File Explorer
                var sortEntry = await Folders.GetSortOrder(file.Path);
                
                // Load folder files
                var folder = await file.GetFolder();
                var files = (await folder.GetFilesAsync()).ToList();

                // Filter audio files only
                var audioFiles = files.Where(f => Files.GetMediaType(f.Path) == MediaType.Audio).ToList();

                // Apply sorting
                audioFiles = ApplySorting(audioFiles);

                // Build playlist
                playlist.Clear();
                int trackNum = 1;
                foreach (var audioFile in audioFiles)
                {
                    var item = new AudioPlaylistItem(audioFile, trackNum++);
                    playlist.Add(item);
                }

                // Find and play the opened file
                var openedItem = playlist.FirstOrDefault(i => i.File.Path == file.Path);
                if (openedItem != null)
                {
                    await PlayItem(openedItem);
                }

                // Update window title
                if (playlist.Count > 1)
                {
                    WindowTitle.Text = $"Playlist ({playlist.Count} tracks)";
                }
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Error Loading Audio");
            }
        }

        private List<StorageFile> ApplySorting(List<StorageFile> files)
        {
            var sortEntry = App.Current.LastSortEntry;

            if (sortEntry == null)
            {
                return files.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList();
            }

            return sortEntry.Value.PropertyName switch
            {
                "System.ItemNameDisplay" => sortEntry.Value.AscendingOrder
                    ? files.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList()
                    : files.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList(),

                "System.DateModified" => sortEntry.Value.AscendingOrder
                    ? files.OrderBy(f => f.DateCreated).ToList()
                    : files.OrderByDescending(f => f.DateCreated).ToList(),

                "System.Size" => sortEntry.Value.AscendingOrder
                    ? files.OrderBy(f => f.GetBasicPropertiesAsync().GetAwaiter().GetResult().Size).ToList()
                    : files.OrderByDescending(f => f.GetBasicPropertiesAsync().GetAwaiter().GetResult().Size).ToList(),

                _ => files.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList()
            };
        }

        #endregion


        #region Playback

        private async Task PlayItem(AudioPlaylistItem item)
        {
            if (item == null) return;

            try
            {
                // Update current item
                if (currentItem != null)
                {
                    currentItem.IsPlaying = false;
                    currentItem.IsSelected = false;
                }

                currentItem = item;
                currentItem.IsPlaying = true;
                currentItem.IsSelected = true;

                // Sync ListView selection
                if (FileList.SelectedItem != item)
                {
                    FileList.SelectedItem = item;
                }

                // Load media
                Player.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(item.File);
                Player.Play();

                // Update properties UI
                currentMediaProperties = item.Properties;
                UpdatePropertiesUI();

                // Load and display album art
                await LoadAlbumArt(item.File);

                // Update transport UI
                TransportTitle.Text = $"{item.DisplayArtist} - {item.DisplayTitle}";
                App.Current.ActiveWindow.Title = TransportTitle.Text;
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Playback Error");
            }
        }

        private void Player_MediaOpened(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Player.NaturalDuration.TotalSeconds > 0)
                {
                    TotalTimeText.Text = FormatTime(TimeSpan.FromSeconds(Player.NaturalDuration.TotalSeconds));
                    ProgressSlider.Maximum = Player.NaturalDuration.TotalSeconds;
                }
                else
                {
                    TotalTimeText.Text = "--:--";
                    ProgressSlider.Maximum = 100;
                }
            });
        }

        private void Player_MediaEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isLooping)
                {
                    Player.Play();
                }
                else
                {
                    UpdatePlayPauseButton(false);
                    ProgressTimer.Stop();
                }
            });
        }


        private void Player_CurrentStateChanged(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatePlayPauseButton(sender.CurrentState == MediaPlayerState.Playing);

                if (sender.CurrentState == MediaPlayerState.Playing)
                {
                    ProgressTimer.Start();
                }
                else if (sender.CurrentState == MediaPlayerState.Paused)
                {
                    ProgressTimer.Stop();
                }
            });
        }

        private void ProgressTimer_Tick(object sender, object e)
        {
            if (!isScrubbing && Player.PlaybackSession != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var currentPosition = Player.PlaybackSession.Position;
                    CurrentTimeText.Text = FormatTime(currentPosition);

                    if (Player.NaturalDuration.TotalSeconds > 0)
                    {
                        ProgressSlider.Value = currentPosition.TotalSeconds;
                    }
                });
            }
        }

        private void ProgressSlider_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Enter scrubbing mode
            isScrubbing = true;

            // Immediately seek to the clicked position (so single clicks jump)
            SeekToPositionFromPointer(e);
        }

        private void ProgressSlider_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // On release we ensure final position is applied and exit scrubbing mode
            SeekToPositionFromPointer(e);
            isScrubbing = false;
        }

        /// <summary>
        /// Calculate slider value from pointer position and apply it to both the slider and media playback session.
        /// This ensures single-clicking the track jumps immediately.
        /// </summary>
        /// <param name="e">Pointer event args from the slider</param>
        private void SeekToPositionFromPointer(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Player == null || Player.Source == null || Player.PlaybackSession == null)
                return;

            // Walk up the visual tree from OriginalSource to find the Slider instance that owns the event.          
            DependencyObject source = e.OriginalSource as DependencyObject;
            Microsoft.UI.Xaml.Controls.Slider slider = null;
            while (source != null)
            {
                if (ReferenceEquals(source, ProgressSlider))
                {
                    slider = ProgressSlider;
                    break;
                }
                if (source is Microsoft.UI.Xaml.Controls.Slider s)
                {
                    slider = s;
                    break;
                }
                source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
            }

            // Fallback: if we couldn't find one in the tree, use the known ProgressSlider instance.
            slider ??= ProgressSlider;

            // Get pointer position relative to the slider
            var p = e.GetCurrentPoint(slider);
            double x = p.Position.X;

            double width = slider.ActualWidth;
            if (width <= 0) width = slider.ActualSize.X;

            // Guard against division by zero
            if (width <= 0) return;

            // Compute ratio and target value
            double ratio = x / width;
            ratio = Math.Clamp(ratio, 0.0, 1.0);

            double newValue = ratio * slider.Maximum;

            // Apply to slider and media
            slider.Value = newValue;

            var newPosition = TimeSpan.FromSeconds(newValue);
            Player.PlaybackSession.Position = newPosition;
            CurrentTimeText.Text = FormatTime(newPosition);
        }





        private void UpdatePlayPauseButton(bool isPlaying)
        {
            if (isPlaying)
            {
                ((FontIcon)PlayPauseButton.Content).Glyph = "\uE769"; // Pause icon
            }
            else
            {
                ((FontIcon)PlayPauseButton.Content).Glyph = "\uE768"; // Play icon
            }
        }

        #endregion

        #region Transport Controls

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayNext();
        }

        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayPrevious();
        }

        private async Task PlayNext()
        {
            if (currentItem == null || playlist.Count == 0) return;

            if (isShuffleEnabled)
            {
                // Shuffle mode
                currentShuffleIndex++;
                
                if (currentShuffleIndex >= shuffleIndices.Count)
                {
                    if (Settings.Current.RepeatMode == RepeatMode.RepeatAll)
                    {
                        GenerateShuffleOrder(); // Regenerate for new shuffle order
                    }
                    else
                    {
                        Player.Pause();
                        Player.PlaybackSession.Position = TimeSpan.Zero;
                        return;
                    }
                }

                int nextIndex = shuffleIndices[currentShuffleIndex];
                await PlayItem(playlist[nextIndex]);
            }
            else
            {
                // Normal sequential mode
                int currentIndex = playlist.IndexOf(currentItem);
                int nextIndex = (currentIndex + 1) % playlist.Count;

                if (nextIndex == 0 && Settings.Current.RepeatMode != RepeatMode.RepeatAll)
                {
                    Player.Pause();
                    Player.PlaybackSession.Position = TimeSpan.Zero;
                    return;
                }

                await PlayItem(playlist[nextIndex]);
            }
        }

        private async Task PlayPrevious()
        {
            if (currentItem == null || playlist.Count == 0) return;

            // If more than 3 seconds into the song, restart it
            if (Player.PlaybackSession.Position.TotalSeconds > 3)
            {
                Player.PlaybackSession.Position = TimeSpan.Zero;
                return;
            }

            if (isShuffleEnabled)
            {
                // Shuffle mode - go to previous in shuffle order
                currentShuffleIndex--;
                
                if (currentShuffleIndex < 0)
                {
                    currentShuffleIndex = shuffleIndices.Count - 1;
                }

                int prevIndex = shuffleIndices[currentShuffleIndex];
                await PlayItem(playlist[prevIndex]);
            }
            else
            {
                // Normal sequential mode
                int currentIndex = playlist.IndexOf(currentItem);
                int prevIndex = currentIndex - 1;
                if (prevIndex < 0) prevIndex = playlist.Count - 1;

                await PlayItem(playlist[prevIndex]);
            }
        }

        private async Task PlayPreviousOld()
        {
            if (currentItem == null || playlist.Count == 0) return;

            // If more than 3 seconds into the song, restart it
            if (Player.PlaybackSession.Position.TotalSeconds > 3)
            {
                Player.PlaybackSession.Position = TimeSpan.Zero;
                return;
            }

            int currentIndex = playlist.IndexOf(currentItem);
            int prevIndex = currentIndex - 1;
            if (prevIndex < 0) prevIndex = playlist.Count - 1;

            await PlayItem(playlist[prevIndex]);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Current.RepeatMode = Settings.Current.RepeatMode switch
            {
                RepeatMode.Off => RepeatMode.RepeatOne,
                RepeatMode.RepeatOne => RepeatMode.RepeatAll,
                RepeatMode.RepeatAll => RepeatMode.Off,
                _ => RepeatMode.Off
            };

            UpdateRepeatButton();
        }

        private void UpdateRepeatButton()
        {
            var icon = RepeatButton.Content as FontIcon;
            if (icon != null)
            {
                icon.Glyph = Settings.Current.RepeatMode switch
                {
                    RepeatMode.RepeatOne => "\uE8ED",
                    RepeatMode.RepeatAll => "\uE8EE",
                    _ => "\uF5E7"
                };
            }
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            isShuffleEnabled = !isShuffleEnabled;
            UpdateShuffleButton();

            if (isShuffleEnabled)
            {
                // Generate shuffle order
                GenerateShuffleOrder();
            }
        }

        private void UpdateShuffleButton()
        {
            var icon = ShuffleButton.Content as FontIcon;
            if (icon != null)
            {
                icon.Foreground = isShuffleEnabled 
                    ? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
                    : new SolidColorBrush(Microsoft.UI.Colors.White);
            }
        }

        private void GenerateShuffleOrder()
        {
            if (playlist.Count == 0) return;

            shuffleIndices.Clear();
            
            // Create list of all indices except current
            int currentIndex = currentItem != null ? playlist.IndexOf(currentItem) : 0;
            for (int i = 0; i < playlist.Count; i++)
            {
                if (i != currentIndex)
                {
                    shuffleIndices.Add(i);
                }
            }

            // Shuffle using Fisher-Yates algorithm
            var random = new Random();
            for (int i = shuffleIndices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shuffleIndices[i], shuffleIndices[j]) = (shuffleIndices[j], shuffleIndices[i]);
            }

            // Add current song at the beginning
            shuffleIndices.Insert(0, currentIndex);
            currentShuffleIndex = 0;
        }

       
     
        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"h\:mm\:ss");
            }
            return time.ToString(@"m\:ss");
        }

        #endregion

        #region Volume Control

        private void VolumeFlyout_Opening(object sender, object e)
        {
            // Find controls within the flyout
            var flyout = sender as Flyout;
            if (flyout?.Content is StackPanel panel)
            {
                var slider = panel.FindName("VolumeSlider") as Slider;
                var percentText = panel.FindName("VolumePercentText") as TextBlock;
                var muteButton = panel.FindName("MuteButton") as Button;
                
                if (slider != null)
                {
                    slider.Value = Player.Volume * 100;
                }
                
                if (percentText != null)
                {
                    percentText.Text = $"{(int)(Player.Volume * 100)}%";
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Player == null) return;

            double volume = e.NewValue / 100.0;
            Player.Volume = volume;
            
            // Update volume percentage text by finding it in the flyout
            if (sender is Slider slider && slider.Parent is Grid grid && grid.Parent is StackPanel panel)
            {
                var percentText = panel.FindName("VolumePercentText") as TextBlock;
                if (percentText != null)
                {
                    percentText.Text = $"{(int)e.NewValue}%";
                }
            }

            // Update volume button icon based on level
            UpdateVolumeIcon(volume);

            // If volume is restored from 0, unmute
            if (isMuted && volume > 0)
            {
                isMuted = false;
                UpdateMuteButton(sender);
            }
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player == null) return;

            // Find the slider in the flyout
            if (sender is Button button && button.Parent is StackPanel panel)
            {
                var slider = panel.FindName("VolumeSlider") as Slider;
                
                if (isMuted)
                {
                    // Unmute
                    Player.Volume = lastVolume;
                    if (slider != null)
                    {
                        slider.Value = lastVolume * 100;
                    }
                    isMuted = false;
                }
                else
                {
                    // Mute
                    lastVolume = Player.Volume;
                    Player.Volume = 0;
                    if (slider != null)
                    {
                        slider.Value = 0;
                    }
                    isMuted = true;
                }

                UpdateMuteButton(sender);
            }
        }

        private void UpdateVolumeIcon(double volume)
        {
            var icon = VolumeButton.Content as FontIcon;
            if (icon != null)
            {
                if (volume == 0)
                {
                    icon.Glyph = "\uE74F"; // Mute icon
                }
                else if (volume < 0.33)
                {
                    icon.Glyph = "\uE992"; // Low volume
                }
                else if (volume < 0.66)
                {
                    icon.Glyph = "\uE993"; // Medium volume
                }
                else
                {
                    icon.Glyph = "\uE995"; // High volume
                }
            }
        }

        private void UpdateMuteButton(object controlInFlyout)
        {
            // Navigate up to find the StackPanel, then find the button
            DependencyObject current = controlInFlyout as DependencyObject;
            StackPanel panel = null;
            
            while (current != null)
            {
                if (current is StackPanel sp)
                {
                    panel = sp;
                    break;
                }
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }
            
            if (panel != null)
            {
                var muteButton = panel.FindName("MuteButton") as Button;
                if (muteButton?.Content is StackPanel buttonPanel)
                {
                    var muteIcon = buttonPanel.FindName("MuteIcon") as FontIcon;
                    var muteText = buttonPanel.FindName("MuteText") as TextBlock;
                    
                    if (muteIcon != null && muteText != null)
                    {
                        if (isMuted)
                        {
                            muteIcon.Glyph = "\uE767"; // Unmute icon
                            muteText.Text = "Unmute";
                        }
                        else
                        {
                            muteIcon.Glyph = "\uE74F"; // Mute icon
                            muteText.Text = "Mute";
                        }
                    }
                }
            }
        }

        #endregion

        #region Album Artwork

        private async Task LoadAlbumArt(StorageFile file)
        {
            try
            {
                using (var tagFile = TagLib.File.Create(file.Path))
                {
                    var pictures = tagFile.Tag.Pictures;
                    if (pictures != null && pictures.Length > 0)
                    {
                        var picture = pictures[0];
                        using (var stream = new InMemoryRandomAccessStream())
                        {
                            await stream.WriteAsync(picture.Data.Data.AsBuffer());
                            stream.Seek(0);

                            var bitmap = new BitmapImage();
                            await bitmap.SetSourceAsync(stream);
                            AlbumArtwork.Source = bitmap;
                            AlbumPlaceholder.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                }

                // No embedded art found
                AlbumArtwork.Source = null;
                AlbumPlaceholder.Visibility = Visibility.Visible;
            }
            catch
            {
                AlbumArtwork.Source = null;
                AlbumPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void AlbumArtwork_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Set as album art";
        }

        private async void AlbumArtwork_Drop(object sender, DragEventArgs e)
        {
            if (currentItem == null) return;

            try
            {
                if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0 && items[0] is StorageFile imageFile)
                    {
                        var mediaType = Files.GetMediaType(imageFile.Path);
                        if (mediaType == MediaType.Image)
                        {
                            await SetAlbumArt(currentItem.File, imageFile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Failed to set album art");
            }
        }

        private async void AlbumArtwork_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (currentItem == null) return;

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.SetOwnerWindow(App.Current.ActiveWindow);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await SetAlbumArt(currentItem.File, file);
            }
        }

        private async Task SetAlbumArt(StorageFile audioFile, StorageFile imageFile)
        {
            try
            {
                using (var tagFile = TagLib.File.Create(audioFile.Path))
                {
                    var imageData = await FileIO.ReadBufferAsync(imageFile);
                    byte[] bytes = new byte[imageData.Length];
                    using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(imageData))
                    {
                        dataReader.ReadBytes(bytes);
                    }

                    var picture = new TagLib.Picture(new TagLib.ByteVector(bytes))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = GetMimeType(imageFile.FileType)
                    };

                    tagFile.Tag.Pictures = new TagLib.IPicture[] { picture };
                    tagFile.Save();
                }

                await LoadAlbumArt(audioFile);
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Failed to save album art");
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "image/jpeg"
            };
        }

        #endregion

        #region Metadata Event Handlers

        private void UpdatePropertiesUI()
        {
            if (currentMediaProperties == null) return;

            isUpdatingUI = true;

            try
            {
                // Update editable fields
                TitleTextBox.Text = currentMediaProperties.Title ?? string.Empty;
                ArtistTextBox.Text = currentMediaProperties.Author ?? string.Empty;
                
                // Album and Genre
                AlbumTextBox.Text = currentMediaProperties.Album ?? string.Empty;
                GenreTextBox.Text = currentMediaProperties.Genre ?? string.Empty;
                
                // Year and Track
                YearNumberBox.Value = currentMediaProperties.Year.HasValue ? currentMediaProperties.Year.Value : double.NaN;
                TrackNumberBox.Value = currentMediaProperties.Track.HasValue ? currentMediaProperties.Track.Value : double.NaN;

                // Comments
                if (!string.IsNullOrEmpty(currentMediaProperties.Comments))
                {
                    CommentsRichEditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, currentMediaProperties.Comments);
                }
                else
                {
                    CommentsRichEditBox.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, string.Empty);
                }

                // Rating - convert from 0-99 scale to 1-5 stars
                if (currentMediaProperties.Rating.HasValue)
                {
                    uint rating = currentMediaProperties.Rating.Value;
                    if (rating == 0) FileRatingControl.Value = -1;
                    else if (rating >= 1 && rating <= 12) FileRatingControl.Value = 1;
                    else if (rating >= 13 && rating <= 37) FileRatingControl.Value = 2;
                    else if (rating >= 38 && rating <= 62) FileRatingControl.Value = 3;
                    else if (rating >= 63 && rating <= 87) FileRatingControl.Value = 4;
                    else if (rating >= 88) FileRatingControl.Value = 5;
                }
                else
                {
                    FileRatingControl.Value = -1;
                }

                // Update read-only file information
                FilePathText.Text = currentMediaProperties.Path ?? string.Empty;
                FileSizeText.Text = currentMediaProperties.Size ?? string.Empty;
                BitRateText.Text = currentMediaProperties.BitDepth ?? string.Empty;
                SampleRateText.Text = currentMediaProperties.DPI ?? string.Empty;

                // Update lyrics
                if (!string.IsNullOrEmpty(currentMediaProperties.Lyrics))
                {
                    LyricsRichEditBox.Document.SetText(TextSetOptions.None, currentMediaProperties.Lyrics);
                }
                else
                {
                    LyricsRichEditBox.Document.SetText(TextSetOptions.None, string.Empty);
                }
            }
            finally
            {
                isUpdatingUI = false;
            }
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            currentMediaProperties.Title = TitleTextBox.Text;
            
            // Update current item display
            if (currentItem != null)
            {
                currentItem.RefreshDisplay();
                TransportTitle.Text = $"{currentItem.DisplayArtist} - {currentItem.DisplayTitle}";
            }
        }

        private void ArtistTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            currentMediaProperties.Author = ArtistTextBox.Text;
            
            // Update current item display
            if (currentItem != null)
            {
                currentItem.RefreshDisplay();
                TransportTitle.Text = $"{currentItem.DisplayArtist} - {currentItem.DisplayTitle}";
            }
        }

        private void AlbumTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            currentMediaProperties.Album = AlbumTextBox.Text;
        }

        private void GenreTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            currentMediaProperties.Genre = GenreTextBox.Text;
        }

        private void YearNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            if (!double.IsNaN(sender.Value))
            {
                currentMediaProperties.Year = (uint)sender.Value;
            }
            else
            {
                currentMediaProperties.Year = null;
            }
        }

        private void TrackNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            if (!double.IsNaN(sender.Value))
            {
                currentMediaProperties.Track = (uint)sender.Value;
            }
            else
            {
                currentMediaProperties.Track = null;
            }
        }

        private void CommentsRichEditBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RichEditBox box && !isUpdatingUI)
            {
                box.LostFocus -= CommentsRichEditBox_LostFocus;
                box.Document.SetText(TextSetOptions.None, string.Empty);

                if (currentMediaProperties?.Comments != null)
                {
                    box.Document.SetText(TextSetOptions.None, currentMediaProperties.Comments);
                }

                box.LostFocus += CommentsRichEditBox_LostFocus;
            }
        }

        private void CommentsRichEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;

            if (sender is RichEditBox box)
            {
                box.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out string value);
                currentMediaProperties.Comments = value;
            }
        }

        private void FileRatingControl_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RatingControl rater && !isUpdatingUI)
            {
                rater.ValueChanged -= FileRatingControl_ValueChanged;
                rater.Value = -1;

                if (currentMediaProperties?.Rating != null)
                {
                    uint rating = currentMediaProperties.Rating.Value;
                    if (rating == 0) rater.Value = -1;
                    else if (rating >= 1 && rating <= 12) rater.Value = 1;
                    else if (rating >= 13 && rating <= 37) rater.Value = 2;
                    else if (rating >= 38 && rating <= 62) rater.Value = 3;
                    else if (rating >= 63 && rating <= 87) rater.Value = 4;
                    else if (rating >= 88) rater.Value = 5;
                }

                rater.ValueChanged += FileRatingControl_ValueChanged;
            }
        }

        private void FileRatingControl_ValueChanged(RatingControl sender, object args)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;

            // Convert 1-5 star rating to 0-99 scale
            if (sender.Value == -1) currentMediaProperties.Rating = 0;
            else if (sender.Value == 1) currentMediaProperties.Rating = 12;
            else if (sender.Value == 2) currentMediaProperties.Rating = 13;
            else if (sender.Value == 3) currentMediaProperties.Rating = 38;
            else if (sender.Value == 4) currentMediaProperties.Rating = 63;
            else if (sender.Value == 5) currentMediaProperties.Rating = 88;
        }

        #endregion

        #region Lyrics Editing

        private void LyricsRichEditBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RichEditBox box && !isUpdatingUI && currentMediaProperties != null)
            {
                box.LostFocus -= LyricsRichEditBox_LostFocus;
                box.Document.SetText(TextSetOptions.None, string.Empty);

                if (!string.IsNullOrEmpty(currentMediaProperties.Lyrics))
                {
                    box.Document.SetText(TextSetOptions.None, currentMediaProperties.Lyrics);
                }

                box.LostFocus += LyricsRichEditBox_LostFocus;
            }
        }

        private void LyricsRichEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;

            if (sender is RichEditBox box)
            {
                box.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out string value);
                currentMediaProperties.Lyrics = value;
            }
        }

        private void ClearLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMediaProperties == null) return;

            LyricsRichEditBox.Document.SetText(TextSetOptions.None, string.Empty);
            currentMediaProperties.Lyrics = null;
        }

        private async void SearchLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMediaProperties == null) return;

            string artist = currentMediaProperties.Author ?? "unknown";
            string title = currentMediaProperties.Title ?? currentItem?.File.DisplayName ?? "unknown";

            // Builder search URL for Genius lyrics
            string searchQuery = $"{artist} {title} lyrics";
            string url = $"https://www.google.com/search?q={Uri.EscapeDataString(searchQuery)}";

            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }

        #endregion

        #region View Management

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool shouldBeNarrow = e.NewSize.Width < 800;
            bool shouldTransportBeNarrow = e.NewSize.Width < 600;
            
            if (shouldBeNarrow != isNarrowView)
            {
                isNarrowView = shouldBeNarrow;
                
                if (isNarrowView)
                {
                    SetupNarrowView();
                }
                else
                {
                    SetupWideView();
                }
            }

            if (shouldTransportBeNarrow != isTransportNarrow)
            {
                isTransportNarrow = shouldTransportBeNarrow;
                
                if (isTransportNarrow)
                {
                    SetupNarrowTransport();
                }
                else
                {
                    SetupWideTransport();
                }
            }
        }

        private void SetupWideView()
        {
            // Show both views side by side
            ViewToggleButtons.Visibility = Visibility.Collapsed;
            ContentSplitter.Visibility = Visibility.Visible;
            
            PlaylistView.Visibility = Visibility.Visible;
            AlbumArtView.Visibility = Visibility.Visible;
            
            PlaylistView.SetValue(Grid.ColumnProperty, 0);
            PlaylistView.ClearValue(Grid.ColumnSpanProperty);
            AlbumArtView.SetValue(Grid.ColumnProperty, 1);
            AlbumArtView.ClearValue(Grid.ColumnSpanProperty);
            
            PlaylistColumn.Width = GridLength.Auto;
            AlbumArtColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        private void SetupNarrowView()
        {
            // Show toggle buttons and current view
            ViewToggleButtons.Visibility = Visibility.Visible;
            ContentSplitter.Visibility = Visibility.Collapsed;
            
            UpdateNarrowView();
        }

        private void SetupWideTransport()
        {
            // Wide transport: Single row with 3 columns
            TransportGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            TransportGrid.RowDefinitions[1].Height = new GridLength(0);
            
            LeftControls.SetValue(Grid.RowProperty, 0);
            LeftControls.SetValue(Grid.ColumnProperty, 0);
            LeftControls.ClearValue(Grid.ColumnSpanProperty);
            LeftControls.HorizontalAlignment = HorizontalAlignment.Left;
            
            CenterControls.SetValue(Grid.RowProperty, 0);
            CenterControls.SetValue(Grid.ColumnProperty, 1);
            CenterControls.SetValue(Grid.ColumnSpanProperty, 1);
            
            RightControls.SetValue(Grid.RowProperty, 0);
            RightControls.SetValue(Grid.ColumnProperty, 2);
            RightControls.HorizontalAlignment = HorizontalAlignment.Right;
            RightControls.Visibility = Visibility.Visible;
            RightControls.Spacing = 12;
            
            // Remove any additional stack panels created for narrow view
            var extraStackPanels = TransportGrid.Children.OfType<StackPanel>()
                .Where(sp => sp != LeftControls && sp != CenterControls && sp != RightControls)
                .ToList();
            foreach (var sp in extraStackPanels)
            {
                // Remove buttons from the extra panel before removing the panel
                sp.Children.Clear();
                TransportGrid.Children.Remove(sp);
            }
            
            // Clear and restore original button order for wide view
            RightControls.Children.Clear();
            RightControls.Children.Add(RepeatButton);
            RightControls.Children.Add(ShuffleButton);
            RightControls.Children.Add(VolumeButton);
            RightControls.Children.Add(MoreButton);
                        
            TransportTitle.Visibility = Visibility.Visible;
            ProgressGrid.Margin = new Thickness(0);
        }

        private void SetupNarrowTransport()
        {
            // Narrow transport: 2 rows
            // Row 0: Timeline spanning full width
            // Row 1: [Shuffle, Repeat] - [Prev, Play, Next] - [Volume, More]
            TransportGrid.RowDefinitions[0].Height = GridLength.Auto;
            TransportGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            
            // Timeline at top spanning all columns
            CenterControls.SetValue(Grid.RowProperty, 0);
            CenterControls.SetValue(Grid.ColumnProperty, 0);
            CenterControls.SetValue(Grid.ColumnSpanProperty, 3);
            
            // Play controls at bottom center
            LeftControls.SetValue(Grid.RowProperty, 1);
            LeftControls.SetValue(Grid.ColumnProperty, 1);
            LeftControls.ClearValue(Grid.ColumnSpanProperty);
            LeftControls.HorizontalAlignment = HorizontalAlignment.Center;
            
            // Right controls split: Shuffle & Repeat on left, Volume & More on right
            RightControls.SetValue(Grid.RowProperty, 1);
            RightControls.SetValue(Grid.ColumnProperty, 0);
            RightControls.ClearValue(Grid.ColumnSpanProperty);
            RightControls.HorizontalAlignment = HorizontalAlignment.Right;
            RightControls.Visibility = Visibility.Visible;
            RightControls.Spacing = 8;
            
            // Remove any existing extra stack panels before creating new one
            var existingExtraPanels = TransportGrid.Children.OfType<StackPanel>()
                .Where(sp => sp != LeftControls && sp != CenterControls && sp != RightControls)
                .ToList();
            foreach (var panel in existingExtraPanels)
            {
                panel.Children.Clear();
                TransportGrid.Children.Remove(panel);
            }
            
            // Reorder buttons: Shuffle, Repeat on left side
            RightControls.Children.Clear();
            RightControls.Children.Add(ShuffleButton);
            RightControls.Children.Add(RepeatButton);
            
            // Create a new stack for Volume and More on the right side
            var rightSideControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8
            };
            rightSideControls.Children.Add(VolumeButton);
            rightSideControls.Children.Add(MoreButton);
            rightSideControls.SetValue(Grid.RowProperty, 1);
            rightSideControls.SetValue(Grid.ColumnProperty, 2);
            
            TransportGrid.Children.Add(rightSideControls);          
            
            TransportTitle.Visibility = Visibility.Collapsed;
            ProgressGrid.Margin = new Thickness(0);
        }

        private void ShowPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (isNarrowView)
            {
                ShowAlbumArtButton.IsChecked = false;
                ShowPlaylistButton.IsChecked = true;
                UpdateNarrowView();
            }
        }

        private void ShowAlbumArtButton_Click(object sender, RoutedEventArgs e)
        {
            if (isNarrowView)
            {
                ShowPlaylistButton.IsChecked = false;
                ShowAlbumArtButton.IsChecked = true;
                UpdateNarrowView();
            }
        }

        private void UpdateNarrowView()
        {
            if (ShowPlaylistButton.IsChecked == true)
            {
                // Show playlist only
                PlaylistView.Visibility = Visibility.Visible;
                AlbumArtView.Visibility = Visibility.Collapsed;
                PlaylistView.SetValue(Grid.ColumnProperty, 0);
                PlaylistView.SetValue(Grid.ColumnSpanProperty, 2);
                PlaylistColumn.Width = new GridLength(1, GridUnitType.Star);
                AlbumArtColumn.Width = new GridLength(0);
            }
            else
            {
                // Show album art only
                PlaylistView.Visibility = Visibility.Collapsed;
                AlbumArtView.Visibility = Visibility.Visible;
                AlbumArtView.SetValue(Grid.ColumnProperty, 0);
                AlbumArtView.SetValue(Grid.ColumnSpanProperty, 2);
                PlaylistColumn.Width = new GridLength(0);
                AlbumArtColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        #endregion

        #region Playlist UI

        private void PlayTrackButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AudioPlaylistItem item)
            {
                _ = PlayItem(item);
            }
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileList.SelectedItem is AudioPlaylistItem item && item != currentItem)
            {
                _ = PlayItem(item);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                string searchText = sender.Text.ToLowerInvariant();
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Show all items
                    FileList.ItemsSource = playlist;
                }
                else
                {
                    // Filter playlist
                    var filtered = playlist.Where(item =>
                        item.DisplayTitle.ToLowerInvariant().Contains(searchText) ||
                        item.DisplayArtist.ToLowerInvariant().Contains(searchText) ||
                        (item.Properties.Album?.ToLowerInvariant().Contains(searchText) ?? false)
                    ).ToList();
                    
                    FileList.ItemsSource = filtered;
                }
            }
        }

        #endregion
    }

    #region Value Converters

    public class SelectedBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelected && isSelected)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray);
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class PlayingIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPlaying && isPlaying)
            {
                return "\uE768"; // Playing icon (sound wave)
            }
            return "\uE768"; // Play icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class PlayingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPlaying && isPlaying)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            }
            return new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
