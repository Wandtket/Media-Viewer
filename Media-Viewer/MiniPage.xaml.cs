using CommunityToolkit.WinUI;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using MediaViewer.Models;
using MediaViewer.Pages;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.Extras;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PlaybackState = NAudio.Wave.PlaybackState;

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

        // NAudio Playback Components
        private IWavePlayer Player;
        private AudioFileReader audioFileReader;
        private VolumeSampleProvider volumeProvider;
        private ReverbSampleProvider reverbProvider;
        private IsolationSampleProvider isolationProvider;
        private ISampleProvider finalSampleProvider;

        private DispatcherTimer ProgressTimer;
        private bool isScrubbing = false;

        // Shuffle
        private bool isShuffleEnabled = false;
        private List<int> shuffleIndices = new List<int>();
        private int currentShuffleIndex = 0;

        // Volume
        private double lastVolume = 1.0;
        private bool isMuted = false;

        // Playback rate
        private float playbackRate = 1.0f; // Normal speed
        private VarispeedSampleProvider varispeedProvider;

        // Repeat delay
        private Slider repeatDelaySliderCache;
        private double repeatDelaySeconds = 0.0; // Delay in seconds before repeating

        // Stereo Pan
        private StereoPanSampleProvider panProvider;
        private float stereoPan = 0.0f; // -1.0 (left) to 1.0 (right)

        // Reverb effect
        private bool isReverbEnabled = false;
        private double reverbAmount = 50.0; // 0-100 scale
        private ToggleSwitch reverbToggleCache;
        private Slider reverbSliderCache;

        // Isolation effect
        private ToggleSwitch isolationToggleCache;
        private bool isIsolationEnabled = false;


        // Metadata saving
        private bool hasUnsavedMetadata = false;

        // Metadata Multi-Select
        private bool isMultiSelectMode = false;
        private string pendingMetadataProperty = null;

        // Playback state tracking
        private bool isPlaying = false;
        private TimeSpan currentPosition = TimeSpan.Zero;
        private TimeSpan totalDuration = TimeSpan.Zero;

        private AudioPlaylistItem lockedItem = null;

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
            App.Current.ActiveWindow.Closed += Window_Closed;

            // Initialize NAudio components
            InitializeAudioPlayer();

            UpdateRepeatButton();

            ProgressTimer = new DispatcherTimer();
            ProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            ProgressTimer.Tick += ProgressTimer_Tick;

            ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ProgressSlider_PointerPressed), true);
            ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);

            UpdatePlayPauseButton(false);

            if (File != null)
            {
                await LoadAudioFile(File);
            }
        }

        private void InitializeAudioPlayer()
        {
            try
            {
                // Initialize WaveOut player
                Player = new WaveOutEvent();
                Player.PlaybackStopped += Player_PlaybackStopped;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize audio player: {ex.Message}");
            }
        }

        private async void Window_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
        {
            if (hasUnsavedMetadata && currentMediaProperties != null)
            {
                args.Handled = true;
                await SaveCurrentMetadata();
                App.Current.ActiveWindow.Closed -= Window_Closed;
                App.Current.ActiveWindow.Close();
            }

            // Cleanup NAudio resources
            CleanupAudioPlayer();
        }

        private void CleanupAudioPlayer()
        {
            try
            {
                ProgressTimer?.Stop();

                if (Player != null)
                {
                    Player.PlaybackStopped -= Player_PlaybackStopped;
                    Player.Stop();
                    Player.Dispose();
                    Player = null;
                }

                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }

                volumeProvider = null;
                reverbProvider = null;
                finalSampleProvider = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during audio cleanup: {ex.Message}");
            }
        }

        #region File Loading

        private async Task LoadAudioFile(StorageFile file)
        {
            try
            {
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

                    // Load duration from file using TagLib
                    try
                    {
                        using (var tagFile = TagLib.File.Create(audioFile.Path))
                        {
                            var duration = tagFile.Properties.Duration;
                            if (duration > TimeSpan.Zero)
                            {
                                item.DisplayDuration = FormatTime(duration);
                            }
                        }
                    }
                    catch
                    {
                        item.DisplayDuration = "--:--";
                    }

                    playlist.Add(item);
                }

                // Find and play the opened file
                var openedItem = playlist.FirstOrDefault(i => i.File.Path == file.Path);
                if (openedItem != null)
                {
                    await PlayItem(openedItem);

                    openedItem.IsSelected = true;
                    FileList.SelectedItem = openedItem;

                    currentMediaProperties = openedItem.Properties;
                    UpdatePropertiesUI();
                }

                WindowTitle.Text = folder.Name;
                UpdatePlaylistInfo();
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
                // Save any pending metadata before switching tracks
                if (hasUnsavedMetadata && currentItem != null)
                {
                    await SaveCurrentMetadata();
                }

                // Update current item
                if (currentItem != null)
                {
                    currentItem.IsPlaying = false;
                    currentItem.IsCurrentTrack = false;
                }

                currentItem = item;
                currentItem.IsPlaying = true;
                currentItem.IsCurrentTrack = true;

                currentMediaProperties = item.Properties;

                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "0:00";

                // Stop current playback
                if (Player != null && Player.PlaybackState != PlaybackState.Stopped)
                {
                    Player.Stop();
                }

                // Dispose previous reader
                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }

                // Load new audio file
                audioFileReader = new AudioFileReader(item.File.Path);
                totalDuration = audioFileReader.TotalTime;

                // Build audio processing chain
                BuildAudioChain();

                // Initialize WaveOut if needed
                if (Player == null)
                {
                    InitializeAudioPlayer();
                }

                // Set the audio source
                Player.Init(finalSampleProvider);

                // Start playback
                Player.Play();
                isPlaying = true;
                ProgressTimer.Start();

                // Update UI
                TotalTimeText.Text = FormatTime(totalDuration);
                ProgressSlider.Maximum = totalDuration.TotalSeconds;
                DurationText.Text = FormatTime(totalDuration);

                if (currentItem != null && (string.IsNullOrEmpty(currentItem.DisplayDuration) || currentItem.DisplayDuration == "00:00"))
                {
                    currentItem.DisplayDuration = FormatTime(totalDuration);
                }

                await LoadAlbumArt(item.File);

                TransportTitle.Text = $"{item.DisplayArtist} - {item.DisplayTitle}";
                App.Current.ActiveWindow.Title = TransportTitle.Text;

                UpdatePropertiesUI();
                UpdatePlayPauseButton(true);
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Playback Error");
                isPlaying = false;
                UpdatePlayPauseButton(false);
            }
        }


        private void BuildAudioChain()
        {
            if (audioFileReader == null) return;

            ISampleProvider chain = audioFileReader;

            // Add playback rate control (before volume so rate change affects all subsequent effects)
            if (playbackRate != 1.0f)
            {
                varispeedProvider = new VarispeedSampleProvider(chain);
                varispeedProvider.PlaybackRate = playbackRate;
                chain = varispeedProvider;
            }
            else
            {
                varispeedProvider = null;
            }

            // Add volume control
            volumeProvider = new VolumeSampleProvider(chain);
            volumeProvider.Volume = (float)lastVolume;
            chain = volumeProvider;

            // Add stereo pan control (before effects so pan affects the effected signal)
            panProvider = new StereoPanSampleProvider(chain);
            panProvider.Pan = stereoPan;
            chain = panProvider;

            // Add isolation effect if enabled (takes priority over reverb)
            if (isIsolationEnabled)
            {
                isolationProvider = new IsolationSampleProvider(chain);
                isolationProvider.Intensity = 1.0f; // Always 100%
                isolationProvider.BassBoost = 0.5f; // Fixed at 50%
                isolationProvider.EnableEffect = true;
                chain = isolationProvider;

                // Disable reverb if isolation is active
                reverbProvider = null;
            }
            // Add reverb effect if enabled (only if isolation is not active)
            else if (isReverbEnabled)
            {
                reverbProvider = new ReverbSampleProvider(chain);
                reverbProvider.ReverbAmount = (float)(reverbAmount / 100.0);
                reverbProvider.EnableEffect = true;
                chain = reverbProvider;

                isolationProvider = null;
            }
            else
            {
                reverbProvider = null;
                isolationProvider = null;
            }

            finalSampleProvider = chain;
        }


        private TextBlock panValueTextBlock = null;

        private void StereoPanSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            // Convert -100 to 100 range to -1.0 to 1.0
            stereoPan = (float)(e.NewValue / 100.0);

            // Update the pan provider if it exists
            if (panProvider != null)
            {
                panProvider.Pan = stereoPan;
            }

            // Cache the text block reference if we haven't already
            if (panValueTextBlock == null && sender is Slider slider)
            {
                // Navigate up to find the parent StackPanel
                DependencyObject parent = slider;
                while (parent != null)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                    if (parent is StackPanel stackPanel)
                    {
                        // Try to find the text block by name within the same panel
                        panValueTextBlock = stackPanel.FindName("PanValueText") as TextBlock;
                        break;
                    }
                }
            }

            // Update the label text using cached reference
            if (panValueTextBlock != null)
            {
                int sliderValue = (int)e.NewValue;
                if (sliderValue == 0)
                {
                    panValueTextBlock.Text = "Center";
                }
                else if (sliderValue < 0)
                {
                    panValueTextBlock.Text = $"L {Math.Abs(sliderValue)}%";
                }
                else
                {
                    panValueTextBlock.Text = $"R {sliderValue}%";
                }
            }
        }

        private void PanCenterButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the slider in the flyout
            if (sender is Button button && button.Parent is Grid grid && grid.Parent is StackPanel panel)
            {
                var slider = panel.FindName("StereoPanSlider") as Slider;
                if (slider != null)
                {
                    slider.Value = 0;
                }
            }
        }

        private void Player_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                if (e.Exception != null)
                {
                    Debug.WriteLine($"Playback stopped with error: {e.Exception.Message}");
                    await ErrorBox.Show(e.Exception, Title: "Playback Error");
                    return;
                }

                isPlaying = false;
                ProgressTimer.Stop();

                // Check if we're near the end of the track
                bool isNearEnd = false;
                if (audioFileReader != null)
                {
                    long bytesFromEnd = audioFileReader.Length - audioFileReader.Position;
                    long bytesPerSecond = audioFileReader.WaveFormat.AverageBytesPerSecond;
                    double secondsFromEnd = (double)bytesFromEnd / bytesPerSecond;

                    double timeThreshold = playbackRate != 1.0f ? 3.0 : 0.5;
                    const long absoluteByteThreshold = 10240; // 10 KB

                    isNearEnd = (secondsFromEnd < timeThreshold) || (bytesFromEnd < absoluteByteThreshold);

                    Debug.WriteLine($"Playback stopped. Bytes from end: {bytesFromEnd}, Seconds from end: {secondsFromEnd:F2}, Time threshold: {timeThreshold}, Rate: {playbackRate}x, IsNearEnd: {isNearEnd}");
                }

                if (audioFileReader != null && isNearEnd)
                {
                    if (Settings.Current.RepeatMode == RepeatMode.RepeatOne)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(repeatDelaySeconds));

                        try
                        {
                            Player.PlaybackStopped -= Player_PlaybackStopped;

                            try
                            {
                                if (Player.PlaybackState != PlaybackState.Stopped)
                                {
                                    Player.Stop();
                                }
                            }
                            catch (Exception stopEx)
                            {
                                Debug.WriteLine($"Error stopping player: {stopEx.Message}");
                            }

                            Player.Dispose();
                            Player = null;

                            await Task.Delay(150);

                            audioFileReader.Position = 0;
                            BuildAudioChain();

                            Player = new WaveOutEvent();
                            Player.PlaybackStopped += Player_PlaybackStopped;

                            Player.Init(finalSampleProvider);

                            await Task.Delay(50);

                            Player.Play();

                            isPlaying = true;
                            ProgressTimer.Start();
                            UpdatePlayPauseButton(true);

                            if (currentItem != null)
                            {
                                currentItem.IsPlaying = true;
                            }

                            Debug.WriteLine("Track restart successful");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error restarting track in RepeatOne mode: {ex.Message}");
                            Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                            try
                            {
                                if (Player != null)
                                {
                                    Player.PlaybackStopped -= Player_PlaybackStopped;
                                    Player.Dispose();
                                }

                                InitializeAudioPlayer();

                                if (audioFileReader != null && currentItem != null)
                                {
                                    audioFileReader.Position = 0;
                                    BuildAudioChain();
                                    Player.Init(finalSampleProvider);
                                }
                            }
                            catch (Exception recoveryEx)
                            {
                                Debug.WriteLine($"Recovery failed: {recoveryEx.Message}");
                            }

                            UpdatePlayPauseButton(false);

                            if (currentItem != null)
                            {
                                currentItem.IsPlaying = false;
                            }
                        }
                    }
                    else if (Settings.Current.RepeatMode == RepeatMode.RepeatAll ||
                             (Settings.Current.RepeatMode == RepeatMode.Off && HasNextTrack()))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(repeatDelaySeconds));

                        await PlayNext();
                    }
                    else
                    {
                        audioFileReader.Position = 0;
                        ProgressSlider.Value = 0;
                        CurrentTimeText.Text = "0:00";
                        UpdatePlayPauseButton(false);

                        if (currentItem != null)
                        {
                            currentItem.IsPlaying = false;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Track was manually stopped (not near end)");
                    UpdatePlayPauseButton(false);

                    if (currentItem != null)
                    {
                        currentItem.IsPlaying = false;
                    }
                }
            });
        }
        
        private bool HasNextTrack()
        {
            if (currentItem == null || playlist.Count == 0) return false;

            if (isShuffleEnabled)
            {
                return currentShuffleIndex < shuffleIndices.Count - 1;
            }
            else
            {
                int currentIndex = playlist.IndexOf(currentItem);
                return currentIndex < playlist.Count - 1;
            }
        }

        private void ProgressTimer_Tick(object sender, object e)
        {
            if (!isScrubbing && audioFileReader != null && Player != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        currentPosition = audioFileReader.CurrentTime;
                        CurrentTimeText.Text = FormatTime(currentPosition);

                        if (totalDuration.TotalSeconds > 0)
                        {
                            ProgressSlider.Value = currentPosition.TotalSeconds;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating progress: {ex.Message}");
                    }
                });
            }
        }

        private void ProgressSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (audioFileReader == null)
                return;

            // If user is actively scrubbing (dragging), update position continuously.
            if (isScrubbing)
            {
                var newPosition = TimeSpan.FromSeconds(e.NewValue);
                try
                {
                    audioFileReader.CurrentTime = newPosition;
                    CurrentTimeText.Text = FormatTime(newPosition);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error seeking: {ex.Message}");
                }
            }
        }

        private void ProgressSlider_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Enter scrubbing mode
            isScrubbing = true;

            // Immediately seek to the clicked position (so single clicks jump)
            SeekToPosition(e);
        }

        private void ProgressSlider_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // On release we ensure final position is applied and exit scrubbing mode
            SeekToPosition(e);
            isScrubbing = false;
        }

        private void SeekToPosition(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (audioFileReader == null || Player == null)
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

            // Temporarily disable ValueChanged handler to prevent double-seeking
            bool wasScrubbing = isScrubbing;
            isScrubbing = false;

            // Apply to slider
            slider.Value = newValue;

            // Restore scrubbing state
            isScrubbing = wasScrubbing;

            // Apply to audio playback
            try
            {
                var newPosition = TimeSpan.FromSeconds(newValue);
                audioFileReader.CurrentTime = newPosition;
                CurrentTimeText.Text = FormatTime(newPosition);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error seeking to position: {ex.Message}");
            }
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

        private void UpdatePlaylistInfo()
        {
            int trackCount = playlist.Count;

            // Calculate total duration
            TimeSpan totalDuration = TimeSpan.Zero;
            foreach (var item in playlist)
            {
                if (!string.IsNullOrEmpty(item.DisplayDuration) && item.DisplayDuration != "--:--")
                {
                    // Parse the duration string manually
                    // Formats: "m:ss" or "h:mm:ss"
                    var parts = item.DisplayDuration.Split(':');

                    try
                    {
                        if (parts.Length == 2)
                        {
                            // Format is "m:ss" (minutes:seconds)
                            int minutes = int.Parse(parts[0]);
                            int seconds = int.Parse(parts[1]);
                            totalDuration += new TimeSpan(0, minutes, seconds);
                        }
                        else if (parts.Length == 3)
                        {
                            // Format is "h:mm:ss" (hours:minutes:seconds)
                            int hours = int.Parse(parts[0]);
                            int minutes = int.Parse(parts[1]);
                            int seconds = int.Parse(parts[2]);
                            totalDuration += new TimeSpan(hours, minutes, seconds);
                        }
                    }
                    catch
                    {
                        // Skip items with invalid duration format
                        Debug.WriteLine($"Failed to parse duration: {item.DisplayDuration}");
                    }
                }
            }

            // Update UI
            TrackCountText.Text = trackCount == 1 ? "1 Track" : $"{trackCount} Tracks";
            TotalDurationText.Text = FormatTime(totalDuration);
        }

        #endregion


        #region Playback Rate

        private async void PlaybackRate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioMenuFlyoutItem menuItem && menuItem.Tag is string rateString)
            {
                if (float.TryParse(rateString, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float newRate))
                {
                    playbackRate = newRate;
                    await ApplyPlaybackRateAsync(); // Make it async
                }
            }
        }

        private async Task ApplyPlaybackRateAsync()
        {
            if (audioFileReader == null || Player == null)
            {
                return;
            }

            try
            {
                Debug.WriteLine($"Applying playback rate: {playbackRate}x");

                bool wasPlaying = Player.PlaybackState == PlaybackState.Playing;
                TimeSpan currentPos = audioFileReader.CurrentTime;

                // Stop and tear down the current player so we can re-init cleanly
                Player.PlaybackStopped -= Player_PlaybackStopped;
                try
                {
                    Player.Stop();
                }
                catch (Exception stopEx)
                {
                    Debug.WriteLine($"Error stopping player before rate change: {stopEx.Message}");
                }
                Player.Dispose();
                Player = null;

                await Task.Delay(80); // allow buffers to release

                // Rebuild the chain with the new rate
                BuildAudioChain();

                // Recreate player and re-subscribe
                Player = new WaveOutEvent();
                Player.PlaybackStopped += Player_PlaybackStopped;
                Player.Init(finalSampleProvider);

                // Restore position
                audioFileReader.CurrentTime = currentPos;

                if (wasPlaying)
                {
                    Player.Play();
                    isPlaying = true;
                    ProgressTimer.Start();
                }
                else
                {
                    isPlaying = false;
                    ProgressTimer.Stop();
                }

                Debug.WriteLine($"Playback rate changed to {playbackRate}x - Success");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying playback rate: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                try
                {
                    if (Player != null)
                    {
                        Player.PlaybackStopped -= Player_PlaybackStopped;
                        Player.Dispose();
                        Player = null;
                    }

                    await Task.Delay(100);

                    InitializeAudioPlayer();

                    if (audioFileReader != null)
                    {
                        BuildAudioChain();
                        Player.Init(finalSampleProvider);
                    }
                }
                catch (Exception recoveryEx)
                {
                    Debug.WriteLine($"Failed to recover from playback rate error: {recoveryEx.Message}");
                    await ErrorBox.Show(recoveryEx, Title: "Playback Rate Error");
                }
            }
        }

        #endregion

        #region Repeat Delay

        private void RepeatDelaySlider_Loaded(object sender, RoutedEventArgs e)
        {
            repeatDelaySliderCache = sender as Slider;

            if (repeatDelaySliderCache != null)
            {
                // Load saved delay value
                repeatDelaySeconds = Settings.Current.RepeatDelaySeconds;
                repeatDelaySliderCache.Value = repeatDelaySeconds;

                // Update the display text
                UpdateRepeatDelayText(repeatDelaySeconds);
            }
        }

        private void RepeatDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            repeatDelaySeconds = e.NewValue;

            // Save to settings
            Settings.Current.RepeatDelaySeconds = repeatDelaySeconds;

            // Update the display text
            UpdateRepeatDelayText(e.NewValue);
        }

        private void UpdateRepeatDelayText(double value)
        {
            // Find the text block - cache it if we haven't already
            if (repeatDelaySliderCache != null && repeatDelaySliderCache.Parent is StackPanel sliderPanel)
            {
                // Navigate to find the RepeatDelayText TextBlock
                DependencyObject parent = sliderPanel;
                while (parent != null)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                    if (parent is StackPanel stackPanel)
                    {
                        var textBlock = stackPanel.FindName("RepeatDelayText") as TextBlock;
                        if (textBlock != null)
                        {
                            textBlock.Text = value == 0 ? "Off" : $"{value:F1}s";
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Transport Controls

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player == null || audioFileReader == null)
                return;

            if (Player.PlaybackState == PlaybackState.Playing)
            {
                Player.Pause();
                isPlaying = false;
                ProgressTimer.Stop();
                UpdatePlayPauseButton(false);

                if (currentItem != null)
                {
                    currentItem.IsPlaying = false;
                }
            }
            else
            {
                // Check if we're at the end or if the player is in a stopped state
                if (audioFileReader.Position >= audioFileReader.Length || Player.PlaybackState == PlaybackState.Stopped)
                {
                    // Reset position to beginning
                    audioFileReader.Position = 0;

                    // Reinitialize the player if it's stopped (not just paused)
                    if (Player.PlaybackState == PlaybackState.Stopped)
                    {
                        try
                        {
                            Player.Init(finalSampleProvider);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reinitializing player: {ex.Message}");
                            return;
                        }
                    }
                }

                Player.Play();
                isPlaying = true;
                ProgressTimer.Start();
                UpdatePlayPauseButton(true);

                if (currentItem != null)
                {
                    currentItem.IsPlaying = true;
                }
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
                        if (Player != null)
                        {
                            Player.Stop();
                        }
                        if (audioFileReader != null)
                        {
                            audioFileReader.Position = 0;
                        }
                        isPlaying = false;
                        UpdatePlayPauseButton(false);
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
                    if (Player != null)
                    {
                        Player.Stop();
                    }
                    if (audioFileReader != null)
                    {
                        audioFileReader.Position = 0;
                    }
                    isPlaying = false;
                    UpdatePlayPauseButton(false);
                    return;
                }

                await PlayItem(playlist[nextIndex]);
            }
        }

        private async Task PlayPrevious()
        {
            if (currentItem == null || playlist.Count == 0) return;

            // If more than 3 seconds into the song, restart it
            if (audioFileReader != null && audioFileReader.CurrentTime.TotalSeconds > 3)
            {
                audioFileReader.CurrentTime = TimeSpan.Zero;
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

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.Current == null) return;

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
            if (RepeatButton == null || Settings.Current == null) return;

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
            if (ShuffleButton == null) return;

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
                    slider.Value = lastVolume * 100;
                }

                if (percentText != null)
                {
                    percentText.Text = $"{(int)(lastVolume * 100)}%";
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            double volume = e.NewValue / 100.0;
            lastVolume = volume;

            if (volumeProvider != null)
            {
                volumeProvider.Volume = (float)volume;
            }

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
            // Find the slider in the flyout
            if (sender is Button button && button.Parent is StackPanel panel)
            {
                var slider = panel.FindName("VolumeSlider") as Slider;

                if (isMuted)
                {
                    // Unmute
                    if (volumeProvider != null)
                    {
                        volumeProvider.Volume = (float)lastVolume;
                    }
                    if (slider != null)
                    {
                        slider.Value = lastVolume * 100;
                    }
                    isMuted = false;
                }
                else
                {
                    // Mute
                    lastVolume = volumeProvider?.Volume ?? 1.0f;
                    if (volumeProvider != null)
                    {
                        volumeProvider.Volume = 0;
                    }
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


        #region Audio Effects

        private void ReverbEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                isReverbEnabled = toggle.IsOn;

                // Auto-disable isolation if reverb is enabled (they conflict)
                if (isReverbEnabled && isIsolationEnabled)
                {
                    isIsolationEnabled = false;

                    // Update the cached toggle if it exists
                    if (isolationToggleCache != null)
                    {
                        isolationToggleCache.IsOn = false;
                    }
                }

                // Update slider state
                UpdateReverbSliderState();

                ApplyReverbEffect();
            }
        }

        private void ReverbAmountSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            reverbAmount = e.NewValue;
            if (reverbProvider != null && isReverbEnabled)
            {
                // Convert 0-100 slider value to 0.0-1.0 range
                reverbProvider.ReverbAmount = (float)(reverbAmount / 100.0);

                // Also update the wet mix for more dramatic effect at higher values
                // This makes the reverb more noticeable as you increase the slider
                reverbProvider.WetMix = Math.Clamp((float)(reverbAmount / 100.0) * 0.6f, 0f, 0.6f);
            }
        }

        private void ApplyReverbEffect()
        {
            if (audioFileReader == null || Player == null) return;

            try
            {
                // Save current playback state
                var wasPlaying = Player.PlaybackState == PlaybackState.Playing;
                var currentPos = audioFileReader.CurrentTime;

                // CRITICAL: Properly dispose and recreate WaveOutEvent to avoid buffer issues
                Player.PlaybackStopped -= Player_PlaybackStopped;
                Player.Stop();
                Player.Dispose();

                // Wait for buffers to clear
                System.Threading.Thread.Sleep(50);

                // Reset audio file reader position
                audioFileReader.Position = 0;

                // Rebuild the audio chain with/without reverb
                BuildAudioChain();

                // Recreate the wave player
                Player = new WaveOutEvent();
                Player.PlaybackStopped += Player_PlaybackStopped;

                // Initialize with the new chain
                Player.Init(finalSampleProvider);

                // Restore position
                audioFileReader.CurrentTime = currentPos;

                // Resume playback if it was playing
                if (wasPlaying)
                {
                    Player.Play();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying reverb effect: {ex.Message}");
        
                // Attempt recovery
                try
                {
                    if (Player != null)
                    {
                        Player.Dispose();
                    }
                    InitializeAudioPlayer();
            
                    if (audioFileReader != null)
                    {
                        BuildAudioChain();
                        Player.Init(finalSampleProvider);
                    }
                }
                catch (Exception recoveryEx)
                {
                    Debug.WriteLine($"Failed to recover from reverb effect error: {recoveryEx.Message}");
                }
            }
        }

        private void IsolationEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                isIsolationEnabled = toggle.IsOn;

                // Auto-disable reverb if isolation is enabled (they conflict)
                if (isIsolationEnabled && isReverbEnabled)
                {
                    isReverbEnabled = false;

                    // Update the cached toggle if it exists
                    if (reverbToggleCache != null)
                    {
                        reverbToggleCache.IsOn = false;
                    }
                }

                // Update slider state
                UpdateReverbSliderState();

                ApplyIsolationEffect();
            }
        }

        private void ApplyIsolationEffect()
        {
            if (audioFileReader == null || Player == null) return;

            try
            {
                // Save current playback state
                var wasPlaying = Player.PlaybackState == PlaybackState.Playing;
                var currentPos = audioFileReader.CurrentTime;

                // CRITICAL: Properly dispose and recreate WaveOutEvent to avoid buffer issues
                Player.PlaybackStopped -= Player_PlaybackStopped;
                Player.Stop();
                Player.Dispose();

                // Wait for buffers to clear
                System.Threading.Thread.Sleep(50);

                // Reset audio file reader position
                audioFileReader.Position = 0;

                // Rebuild the audio chain with/without isolation
                BuildAudioChain();

                // Recreate the wave player
                Player = new WaveOutEvent();
                Player.PlaybackStopped += Player_PlaybackStopped;

                // Initialize with the new chain
                Player.Init(finalSampleProvider);

                // Restore position
                audioFileReader.CurrentTime = currentPos;

                // Resume playback if it was playing
                if (wasPlaying)
                {
                    Player.Play();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying isolation effect: {ex.Message}");
        
                // Attempt recovery
                try
                {
                    if (Player != null)
                    {
                        Player.Dispose();
                    }
                    InitializeAudioPlayer();
            
                    if (audioFileReader != null)
                    {
                        BuildAudioChain();
                        Player.Init(finalSampleProvider);
                    }
                }
                catch (Exception recoveryEx)
                {
                    Debug.WriteLine($"Failed to recover from isolation effect error: {recoveryEx.Message}");
                }
            }
        }

        private void UpdateReverbSliderState()
        {
            // Update the reverb slider enabled state if it exists in cache
            if (reverbSliderCache != null)
            {
                // Disable reverb slider when isolation is enabled OR when reverb toggle is off
                reverbSliderCache.IsEnabled = !isIsolationEnabled && isReverbEnabled;
            }
        }

        private void ReverbEnabledToggle_Loaded(object sender, RoutedEventArgs e)
        {
            reverbToggleCache = sender as ToggleSwitch;
        }

        private void IsolationEnabledToggle_Loaded(object sender, RoutedEventArgs e)
        {
            isolationToggleCache = sender as ToggleSwitch;
        }

        private void ReverbAmountSlider_Loaded(object sender, RoutedEventArgs e)
        {
            reverbSliderCache = sender as Slider;
            // Update initial state
            UpdateReverbSliderState();
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
                // Save current playback state
                var wasPlaying = Player?.PlaybackState == PlaybackState.Playing;
                var currentPos = audioFileReader?.CurrentTime ?? TimeSpan.Zero;

                // Release the file
                if (Player != null)
                {
                    Player.Stop();
                }

                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }

                await Task.Delay(100);

                using (var tagFile = TagLib.File.Create(audioFile.Path))
                {
                    var imageData = await Windows.Storage.FileIO.ReadBufferAsync(imageFile);
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

                // Reload the track
                audioFileReader = new AudioFileReader(audioFile.Path);
                BuildAudioChain();
                Player.Init(finalSampleProvider);

                await Task.Delay(100);

                if (currentPos > TimeSpan.Zero)
                {
                    audioFileReader.CurrentTime = currentPos;
                }

                if (wasPlaying)
                {
                    Player.Play();
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


        #region Metadata Saving

        private void MarkMetadataChanged()
        {
            if (isUpdatingUI) return;
            hasUnsavedMetadata = true;
        }

        private async Task SaveCurrentMetadata()
        {
            if (!hasUnsavedMetadata || currentMediaProperties == null)
            {
                return;
            }

            try
            {
                // Find which item owns the metadata being saved
                var itemToSave = playlist.FirstOrDefault(i => i.Properties == currentMediaProperties);

                if (itemToSave == null)
                {
                    return;
                }

                // Only pause/release the player if we're saving the currently playing track
                bool wasPlaying = false;
                TimeSpan currentPosition = TimeSpan.Zero;
                bool needsPlayerReload = itemToSave == currentItem;

                if (needsPlayerReload)
                {
                    wasPlaying = Player?.PlaybackState == PlaybackState.Playing;
                    currentPosition = audioFileReader?.CurrentTime ?? TimeSpan.Zero;

                    // Release the file
                    if (Player != null)
                    {
                        Player.Stop();
                    }

                    if (audioFileReader != null)
                    {
                        audioFileReader.Dispose();
                        audioFileReader = null;
                    }

                    await Task.Delay(100);
                }

                // Save metadata to the correct file
                using (var tagFile = TagLib.File.Create(itemToSave.File.Path))
                {
                    tagFile.Tag.Title = currentMediaProperties.Title;

                    // CRITICAL: Save to BOTH Performers (contributing artist) AND AlbumArtists (album artist)
                    if (!string.IsNullOrEmpty(currentMediaProperties.Author))
                    {
                        tagFile.Tag.Performers = new[] { currentMediaProperties.Author };
                        tagFile.Tag.AlbumArtists = new[] { currentMediaProperties.Author };
                    }
                    else
                    {
                        tagFile.Tag.Performers = Array.Empty<string>();
                        tagFile.Tag.AlbumArtists = Array.Empty<string>();
                    }

                    tagFile.Tag.Album = currentMediaProperties.Album;
                    tagFile.Tag.Genres = !string.IsNullOrEmpty(currentMediaProperties.Genre)
                        ? new[] { currentMediaProperties.Genre }
                        : Array.Empty<string>();
                    tagFile.Tag.Year = currentMediaProperties.Year ?? 0;
                    tagFile.Tag.Track = currentMediaProperties.Track ?? 0;
                    tagFile.Tag.Comment = currentMediaProperties.Comments;

                    // CRITICAL: Always save lyrics, even if empty/null
                    // Setting to empty string clears the lyrics tag
                    tagFile.Tag.Lyrics = currentMediaProperties.Lyrics ?? string.Empty;

                    tagFile.Save();
                }

                // Reload metadata from file
                var reloadedProperties = new MediaProperties(itemToSave.File);

                // Wait for properties to load
                await Task.Delay(200);

                // Replace the properties in the item
                itemToSave.Properties = reloadedProperties;

                // CRITICAL: Update currentMediaProperties reference to point to the NEW properties
                // This ensures we're always tracking the correct item's metadata
                currentMediaProperties = reloadedProperties;

                // Only reload player if we were editing the currently playing track
                if (needsPlayerReload)
                {
                    // Reload the track
                    audioFileReader = new AudioFileReader(itemToSave.File.Path);
                    BuildAudioChain();
                    Player.Init(finalSampleProvider);

                    await Task.Delay(100);

                    // Restore playback position
                    if (audioFileReader != null && currentPosition > TimeSpan.Zero)
                    {
                        audioFileReader.CurrentTime = currentPosition;
                    }

                    // Resume playback if it was playing
                    if (wasPlaying)
                    {
                        Player.Play();
                    }
                }

                // Update UI with reloaded values
                itemToSave.RefreshDisplay();

                // Update transport UI if this is the current item
                if (itemToSave == currentItem)
                {
                    TransportTitle.Text = $"{itemToSave.DisplayArtist} - {itemToSave.DisplayTitle}";
                    App.Current.ActiveWindow.Title = TransportTitle.Text;
                }

                hasUnsavedMetadata = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save metadata: {ex.Message}");

                // Attempt to restore playback even if save failed (only if current item was affected)
                var itemToSave = playlist.FirstOrDefault(i => i.Properties == currentMediaProperties);
                if (itemToSave == currentItem)
                {
                    try
                    {
                        audioFileReader = new AudioFileReader(currentItem.File.Path);
                        BuildAudioChain();
                        Player.Init(finalSampleProvider);
                        Player.Play();
                    }
                    catch
                    {
                        // Silent failure - player will remain stopped
                    }
                }
            }
        }

        private void ApplyMetadataToSelected_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string propertyName)
            {
                if (!isMultiSelectMode)
                {
                    // Enter multi-select mode
                    EnterMultiSelectMode(propertyName);
                }
                else
                {
                    // Apply the metadata to selected items
                    ApplyMetadataToSelectedItems(propertyName);
                }
            }
        }

        private void EnterMultiSelectMode(string propertyName)
        {
            isMultiSelectMode = true;
            pendingMetadataProperty = propertyName;
            lockedItem = currentItem; // Lock the current item

            // Switch ListView to multi-select
            FileList.SelectionMode = ListViewSelectionMode.Multiple;

            // Update UI to show we're in selection mode
            UpdateMultiSelectUI(true);

            // Show info bar or notification
            ShowMultiSelectNotification($"Select tracks to apply {propertyName} to, then click 'Apply' again");
        }

        private async void ApplyMetadataToSelectedItems(string propertyName)
        {
            Debug.WriteLine($"ApplyMetadataToSelectedItems called for property: {propertyName}");
            Debug.WriteLine($"  Selected items count: {FileList.SelectedItems.Count}");

            if (FileList.SelectedItems.Count == 0)
            {
                await ErrorBox.Show(new Exception("No items selected"), Title: "Selection Required");
                ExitMultiSelectMode();
                return;
            }

            try
            {
                // CRITICAL: Capture the value from the currently displayed UI (currentMediaProperties)
                // This ensures we get the latest value at the moment of applying
                object valueToApply = propertyName switch
                {
                    "Album" => currentMediaProperties?.Album,
                    "Artist" => currentMediaProperties?.Author,
                    "Genre" => currentMediaProperties?.Genre,
                    "Year" => currentMediaProperties?.Year,
                    "Track" => currentMediaProperties?.Track,
                    "Comments" => currentMediaProperties?.Comments,
                    "Lyrics" => currentMediaProperties?.Lyrics,
                    _ => null
                };

                Debug.WriteLine($"  Value to apply: {valueToApply}");
                Debug.WriteLine($"  Current media properties owner: {playlist.FirstOrDefault(i => i.Properties == currentMediaProperties)?.File.Name}");

                var selectedItems = FileList.SelectedItems.Cast<AudioPlaylistItem>().ToList();

                foreach (var item in selectedItems)
                {
                    Debug.WriteLine($"  Processing item: {item.File.Name}");

                    // Apply the metadata value to each selected item
                    switch (propertyName)
                    {
                        case "Album":
                            Debug.WriteLine($"    Old album: {item.Properties.Album}");
                            item.Properties.Album = valueToApply as string;
                            Debug.WriteLine($"    New album: {item.Properties.Album}");
                            break;
                        case "Artist":
                            Debug.WriteLine($"    Old artist: {item.Properties.Author}");
                            item.Properties.Author = valueToApply as string;
                            Debug.WriteLine($"    New artist: {item.Properties.Author}");
                            break;
                        case "Genre":
                            Debug.WriteLine($"    Old genre: {item.Properties.Genre}");
                            item.Properties.Genre = valueToApply as string;
                            Debug.WriteLine($"    New genre: {item.Properties.Genre}");
                            break;
                        case "Year":
                            item.Properties.Year = valueToApply as uint?;
                            break;
                        case "Track":
                            item.Properties.Track = valueToApply as uint?;
                            break;
                        case "Comments":
                            item.Properties.Comments = valueToApply as string;
                            break;
                        case "Lyrics":
                            item.Properties.Lyrics = valueToApply as string;
                            break;
                    }

                    // Save metadata for this item immediately
                    Debug.WriteLine($"  About to save item: {item.File.Name}");
                    await SaveMetadataForItem(item);
                    Debug.WriteLine($"  Finished saving item: {item.File.Name}");

                    // Update display - WRAP IN TRY/CATCH to prevent crashes
                    try
                    {
                        Debug.WriteLine($"  Refreshing display for: {item.File.Name}");
                        item.RefreshDisplay();
                        Debug.WriteLine($"  Display refreshed for: {item.File.Name}");
                    }
                    catch (Exception refreshEx)
                    {
                        Debug.WriteLine($"  Failed to refresh display: {refreshEx.Message}");
                        // Continue even if refresh fails - the metadata is already saved
                    }
                }

                Debug.WriteLine($"All items processed. Showing notification...");
                ShowMultiSelectNotification($"{propertyName} applied to {selectedItems.Count} track(s)");
                Debug.WriteLine($"Notification shown. Exiting multi-select mode...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ApplyMetadataToSelectedItems: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await ErrorBox.Show(ex, Title: "Error Applying Metadata");
            }
            finally
            {
                Debug.WriteLine($"In finally block, calling ExitMultiSelectMode...");
                ExitMultiSelectMode();
                Debug.WriteLine($"ExitMultiSelectMode completed");
            }
        }

        private void ExitMultiSelectMode()
        {
            isMultiSelectMode = false;
            pendingMetadataProperty = null;
            lockedItem = null; // Clear the lock

            // Return to single selection mode
            FileList.SelectionMode = ListViewSelectionMode.Single;

            // Restore current item selection
            if (currentItem != null)
            {
                FileList.SelectedItem = currentItem;
            }

            // Update UI
            UpdateMultiSelectUI(false);
        }

        private void UpdateMultiSelectUI(bool isMultiSelect)
        {
            // Update button text/enabled state for all apply buttons
            if (ApplyAlbumButton != null)
            {
                ApplyAlbumButton.Content = isMultiSelect && pendingMetadataProperty == "Album"
                    ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new FontIcon { Glyph = "\uE73E" }, new TextBlock { Text = "Apply Now" } } }
                    : new FontIcon { Glyph = "\uE762" };
                ApplyAlbumButton.IsEnabled = !isMultiSelect || pendingMetadataProperty == "Album";
            }

            if (ApplyArtistButton != null)
            {
                ApplyArtistButton.Content = isMultiSelect && pendingMetadataProperty == "Artist"
                    ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new FontIcon { Glyph = "\uE73E" }, new TextBlock { Text = "Apply Now" } } }
                    : new FontIcon { Glyph = "\uE762" };
                ApplyArtistButton.IsEnabled = !isMultiSelect || pendingMetadataProperty == "Artist";
            }

            if (ApplyGenreButton != null)
            {
                ApplyGenreButton.Content = isMultiSelect && pendingMetadataProperty == "Genre"
                    ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new FontIcon { Glyph = "\uE73E" }, new TextBlock { Text = "Apply Now" } } }
                    : new FontIcon { Glyph = "\uE762" };
                ApplyGenreButton.IsEnabled = !isMultiSelect || pendingMetadataProperty == "Genre";
            }

            // Disable editing controls while in multi-select mode
            TitleTextBox.IsEnabled = !isMultiSelect;
            ArtistTextBox.IsEnabled = !isMultiSelect;
            AlbumTextBox.IsEnabled = !isMultiSelect;
            GenreTextBox.IsEnabled = !isMultiSelect;
            YearNumberBox.IsEnabled = !isMultiSelect;
            TrackNumberBox.IsEnabled = !isMultiSelect;
            CommentsRichEditBox.IsEnabled = !isMultiSelect;
            LyricsRichEditBox.IsEnabled = !isMultiSelect;
            FileRatingControl.IsEnabled = !isMultiSelect;

            // Disable album art editing
            AlbumArtwork.AllowDrop = !isMultiSelect;
            AlbumArtwork.IsTapEnabled = !isMultiSelect;

            // Show/hide cancel button in header
            if (CancelSelectionButton != null)
            {
                CancelSelectionButton.Visibility = isMultiSelect ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ShowMultiSelectNotification(string message)
        {
            // You can use an InfoBar or TeachingTip in your XAML
            // For now, just update window title as feedback
            var originalTitle = App.Current.ActiveWindow.Title;
            App.Current.ActiveWindow.Title = $"[SELECTION MODE] {message}";

            // Restore title after a few seconds
            Task.Delay(3000).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (App.Current.ActiveWindow.Title.StartsWith("[SELECTION MODE]"))
                    {
                        App.Current.ActiveWindow.Title = originalTitle;
                    }
                });
            });
        }

        private void CancelSelection_Click(object sender, RoutedEventArgs e)
        {
            ExitMultiSelectMode();
        }

        private async Task SaveMetadataForItem(AudioPlaylistItem item)
        {
            try
            {
                // Add debug logging
                Debug.WriteLine($"SaveMetadataForItem called for: {item.File.Name}");
                Debug.WriteLine($"  Current Artist: {item.Properties.Author}");

                bool needsPlayerReload = item == currentItem;
                bool wasPlaying = false;
                TimeSpan currentPosition = TimeSpan.Zero;

                // Only pause/release player if we're saving the currently playing track
                if (needsPlayerReload)
                {
                    Debug.WriteLine($"  Pausing player for current item");
                    wasPlaying = Player?.PlaybackState == PlaybackState.Playing;
                    currentPosition = audioFileReader?.CurrentTime ?? TimeSpan.Zero;

                    if (Player != null)
                    {
                        Player.Stop();
                    }

                    if (audioFileReader != null)
                    {
                        audioFileReader.Dispose();
                        audioFileReader = null;
                    }

                    await Task.Delay(100);
                }

                // CRITICAL: Save to disk immediately, regardless of whether it's the current item
                Debug.WriteLine($"  Saving to disk...");
                using (var tagFile = TagLib.File.Create(item.File.Path))
                {
                    tagFile.Tag.Title = item.Properties.Title;

                    // CRITICAL: Save to BOTH Performers (contributing artist) AND AlbumArtists (album artist)
                    // This ensures compatibility with all media players and File Explorer
                    if (!string.IsNullOrEmpty(item.Properties.Author))
                    {
                        tagFile.Tag.Performers = new[] { item.Properties.Author };
                        tagFile.Tag.AlbumArtists = new[] { item.Properties.Author };
                        Debug.WriteLine($"  Set both Performers and AlbumArtists to: {item.Properties.Author}");
                    }
                    else
                    {
                        tagFile.Tag.Performers = Array.Empty<string>();
                        tagFile.Tag.AlbumArtists = Array.Empty<string>();
                        Debug.WriteLine($"  Cleared both Performers and AlbumArtists");
                    }

                    tagFile.Tag.Album = item.Properties.Album;
                    tagFile.Tag.Genres = !string.IsNullOrEmpty(item.Properties.Genre)
                        ? new[] { item.Properties.Genre }
                        : Array.Empty<string>();
                    tagFile.Tag.Year = item.Properties.Year ?? 0;
                    tagFile.Tag.Track = item.Properties.Track ?? 0;
                    tagFile.Tag.Comment = item.Properties.Comments;
                    tagFile.Tag.Lyrics = item.Properties.Lyrics ?? string.Empty;

                    tagFile.Save();
                    Debug.WriteLine($"  Successfully saved to disk");
                }

                // CRITICAL: For the currently selected item (the one being viewed in UI),
                // update currentMediaProperties reference to ensure UI stays in sync
                if (item.Properties == currentMediaProperties)
                {
                    Debug.WriteLine($"  This is the current item in UI");
                }

                // Reload player only if this was the currently playing track
                if (needsPlayerReload)
                {
                    Debug.WriteLine($"  Reloading player");
                    audioFileReader = new AudioFileReader(item.File.Path);
                    BuildAudioChain();
                    Player.Init(finalSampleProvider);

                    await Task.Delay(100);

                    if (audioFileReader != null && currentPosition > TimeSpan.Zero)
                    {
                        audioFileReader.CurrentTime = currentPosition;
                    }

                    if (wasPlaying)
                    {
                        Player.Play();
                    }
                }

                Debug.WriteLine($"SaveMetadataForItem completed successfully for: {item.File.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save metadata for {item.File.Name}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
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

                // Update duration from current item or audio file
                if (currentItem != null && !string.IsNullOrEmpty(currentItem.DisplayDuration) && currentItem.DisplayDuration != "00:00")
                {
                    DurationText.Text = currentItem.DisplayDuration;
                }
                else if (totalDuration.TotalSeconds > 0)
                {
                    DurationText.Text = FormatTime(totalDuration);
                }
                else
                {
                    DurationText.Text = "--:--";
                }

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

        private void MetaTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;
            currentMediaProperties.Title = TitleTextBox.Text;
            currentMediaProperties.Author = ArtistTextBox.Text;
            currentMediaProperties.Album = AlbumTextBox.Text;
            currentMediaProperties.Genre = GenreTextBox.Text;

            // Find which item owns the current properties being edited
            var editedItem = playlist.FirstOrDefault(i => i.Properties == currentMediaProperties);

            // Update the item's display
            if (editedItem != null)
            {
                editedItem.RefreshDisplay();

                // If this is also the currently playing item, update transport
                if (editedItem == currentItem)
                {
                    TransportTitle.Text = $"{editedItem.DisplayArtist} - {editedItem.DisplayTitle}";
                    App.Current.ActiveWindow.Title = TransportTitle.Text;
                }
            }

            MarkMetadataChanged();
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
            MarkMetadataChanged();
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
            MarkMetadataChanged();
        }

        private void CommentsRichEditBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RichEditBox box && !isUpdatingUI)
            {
                box.TextChanged -= CommentsRichEditBox_TextChanged;
                box.Document.SetText(TextSetOptions.None, string.Empty);

                if (currentMediaProperties?.Comments != null)
                {
                    box.Document.SetText(TextSetOptions.None, currentMediaProperties.Comments);
                }

                box.TextChanged += CommentsRichEditBox_TextChanged;
            }
        }

        private void CommentsRichEditBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;

            if (sender is RichEditBox box)
            {
                box.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out string value);
                currentMediaProperties.Comments = value;
                MarkMetadataChanged();
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

            MarkMetadataChanged();
        }

        #endregion


        #region Lyrics Editing

        private void LyricsRichEditBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RichEditBox box && !isUpdatingUI && currentMediaProperties != null)
            {
                box.TextChanged -= LyricsRichEditBox_TextChanged;
                box.Document.SetText(TextSetOptions.None, string.Empty);

                if (!string.IsNullOrEmpty(currentMediaProperties.Lyrics))
                {
                    box.Document.SetText(TextSetOptions.None, currentMediaProperties.Lyrics);
                }

                box.TextChanged += LyricsRichEditBox_TextChanged;
            }
        }

        private void LyricsRichEditBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (isUpdatingUI || currentMediaProperties == null) return;

            if (sender is RichEditBox box)
            {
                box.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out string value);
                currentMediaProperties.Lyrics = value;
                MarkMetadataChanged();
            }
        }

        private void ClearLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMediaProperties == null) return;

            // Clear the lyrics in the UI
            LyricsRichEditBox.Document.SetText(TextSetOptions.None, string.Empty);

            // Set to empty string (not null) to ensure it's saved as cleared
            currentMediaProperties.Lyrics = string.Empty;

            MarkMetadataChanged();
        }

        private async void SearchLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMediaProperties == null) return;

            string artist = currentMediaProperties.Author ?? "unknown";
            string title = currentMediaProperties.Title ?? currentItem?.File.DisplayName ?? "unknown";

            // Build search URL for Genius lyrics
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
            MetaDataView.Visibility = Visibility.Visible;

            PlaylistView.SetValue(Grid.ColumnProperty, 0);
            PlaylistView.ClearValue(Grid.ColumnSpanProperty);
            MetaDataView.SetValue(Grid.ColumnProperty, 1);
            MetaDataView.ClearValue(Grid.ColumnSpanProperty);

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
                MetaDataView.Visibility = Visibility.Collapsed;
                PlaylistView.SetValue(Grid.ColumnProperty, 0);
                PlaylistView.SetValue(Grid.ColumnSpanProperty, 2);
                PlaylistColumn.Width = new GridLength(1, GridUnitType.Star);
                AlbumArtColumn.Width = new GridLength(0);
            }
            else
            {
                // Show album art only
                PlaylistView.Visibility = Visibility.Collapsed;
                MetaDataView.Visibility = Visibility.Visible;
                MetaDataView.SetValue(Grid.ColumnProperty, 0);
                MetaDataView.SetValue(Grid.ColumnSpanProperty, 2);
                PlaylistColumn.Width = new GridLength(0);
                AlbumArtColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        #endregion


        #region Playlist UI

        private async void PlayTrackButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent playing different tracks while in multi-select mode
            if (isMultiSelectMode)
            {
                return;
            }

            if (sender is Button button && button.Tag is AudioPlaylistItem item)
            {
                // If clicking the currently playing item, toggle play/pause
                if (item == currentItem && item.IsPlaying)
                {
                    // Item is currently playing, so pause it
                    if (Player != null && Player.PlaybackState == PlaybackState.Playing)
                    {
                        Player.Pause();
                        isPlaying = false;
                        ProgressTimer.Stop();
                        UpdatePlayPauseButton(false);
                        item.IsPlaying = false;
                    }
                    else
                    {
                        // Resume playback
                        Player?.Play();
                        isPlaying = true;
                        ProgressTimer.Start();
                        UpdatePlayPauseButton(true);
                        item.IsPlaying = true;
                    }
                }
                else
                {
                    // Update selected item flag for all items
                    foreach (var playlistItem in playlist)
                    {
                        playlistItem.IsSelected = playlistItem == item;
                    }

                    // Update ListView selection to match
                    FileList.SelectedItem = item;

                    // Update metadata UI and album art for the new selection
                    currentMediaProperties = item.Properties;
                    UpdatePropertiesUI();
                    await LoadAlbumArt(item.File);

                    // Play the selected item
                    await PlayItem(item);
                }
            }
        }

        private async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent changing selection while in multi-select mode
            if (isMultiSelectMode)
            {
                // Restore the current item as selected in single-select terms
                // but don't interfere with multi-select checkboxes
                return;
            }

            if (FileList.SelectedItem is AudioPlaylistItem item)
            {
                // Update selected item flag for all items
                foreach (var playlistItem in playlist)
                {
                    playlistItem.IsSelected = playlistItem == item;
                }

                // Update metadata UI without changing playback
                currentMediaProperties = item.Properties;
                UpdatePropertiesUI();

                // Load and display album art for the selected item
                await LoadAlbumArt(item.File);
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
                return "\uE769"; // Pause icon
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
            // Now checking IsCurrentTrack instead of IsPlaying
            // This keeps the track blue whether it's playing or paused
            if (value is bool isCurrentTrack && isCurrentTrack)
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

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isEnabled && isEnabled)
            {
                return 1.0;
            }
            return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}