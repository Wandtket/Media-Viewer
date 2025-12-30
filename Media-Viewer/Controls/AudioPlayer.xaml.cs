using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Controls
{
    public sealed partial class AudioPlayer : UserControl
    {
        private Windows.Media.Playback.MediaPlayer Player;
        private DispatcherTimer ProgressTimer;
        private bool isScrubbing = false;
        private bool isLooping = false;

        public AudioPlayer()
        {
            InitializeComponent();
            InitializePlayer();
        }

        private void InitializePlayer()
        {
            // Create media player
            Player = new Windows.Media.Playback.MediaPlayer()
            {
                AutoPlay = true
            };

            // Set up event handlers
            Player.MediaOpened += MediaPlayer_MediaOpened;
            Player.MediaEnded += MediaPlayer_MediaEnded;
            Player.CurrentStateChanged += MediaPlayer_CurrentStateChanged;

            // Set up progress timer
            ProgressTimer = new DispatcherTimer();
            ProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            ProgressTimer.Tick += ProgressTimer_Tick;

            ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ProgressSlider_PointerPressed), true);
            ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);

            // Update UI for initial state
            UpdatePlayPauseButton(true);
        }
        

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
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

        private void MediaPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
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

        private void MediaPlayer_CurrentStateChanged(Windows.Media.Playback.MediaPlayer sender, object args)
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

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();

            // Initialize the file picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.FileTypeFilter.Add(".m4a");
            filePicker.FileTypeFilter.Add(".wma");

            var file = await filePicker.PickSingleFileAsync();

            if (file != null)
            {
                await LoadAudioFile(file);
            }
        }

        public async Task LoadAudioFile(StorageFile file)
        {
            var mediaSource = MediaSource.CreateFromStorageFile(file);
            Player.Source = mediaSource;

            // Reset UI
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            UpdatePlayPauseButton(false);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Player.Source == null)
                return;

            if (Player.CurrentState == MediaPlayerState.Playing)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
            }
        }

        private void ProgressSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (Player.Source == null)
                return;

            // If user is actively scrubbing (dragging), update position continuously.
            if (isScrubbing)
            {
                var newPosition = TimeSpan.FromSeconds(e.NewValue);
                if (Player.PlaybackSession != null)
                {
                    Player.PlaybackSession.Position = newPosition;
                }

                CurrentTimeText.Text = FormatTime(newPosition);
            }
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            isLooping = !isLooping;

            // Update loop icon
            if (isLooping) LoopIcon.Glyph = "\uE8ED";
            else LoopIcon.Glyph = "\uE8EE"; 

            // Set media player loop
            Player.IsLoopingEnabled = isLooping;
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


        private string FormatTime(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalMinutes:00}:{timeSpan.Seconds:00}";
        }

    }
}
