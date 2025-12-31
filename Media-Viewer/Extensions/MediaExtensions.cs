using FFMpegCore;
using MediaViewer.Enums;
using MediaViewer.Models;
using MediaViewer.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaViewer.Extensions
{


    public static class MediaExtensions
    {

        public static async void OpenFile()
        {

        }


        /// <summary>
        /// Extracts chapters from a video file using FFMpegCore.
        /// </summary>
        public static async Task<List<ChapterInfo>> ExtractChaptersAsync(string videoPath)
        {
            var chapters = new List<ChapterInfo>();

            try
            {
                // Use FFProbe to analyze the video file
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);

                if (mediaInfo.Chapters != null && mediaInfo.Chapters.Any())
                {
                    foreach (var chapter in mediaInfo.Chapters)
                    {
                        var chapterInfo = new ChapterInfo
                        {
                            StartTime = chapter.Start,
                            Title = !string.IsNullOrWhiteSpace(chapter.Title)
                                ? chapter.Title
                                : $"Chapter {chapters.Count + 1}"
                        };

                        chapters.Add(chapterInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting chapters: {ex.Message}");
            }

            return chapters;
        }

        public static async Task<TimeSpan> GetVideoDurationAsync(string videoPath)
        {
            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
                return mediaInfo.Duration;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting video duration: {ex.Message}");
                return TimeSpan.Zero;
            }
        }



        public static void RunTopazVideoAI(string inputPath)
        {
            var topazExePath = @"C:\Program Files\Topaz Labs LLC\Topaz Video AI\Topaz Video AI.exe";
            var args = $"\"{inputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = topazExePath,
                Arguments = args,
                UseShellExecute = true, // Show the Topaz GUI
                CreateNoWindow = false
            };

            var process = new Process { StartInfo = psi };
            process.Start();
        }


    }

    public sealed class CustomMediaTransportControls : MediaTransportControls
    {
        public event EventHandler<EventArgs> Liked;

        public event EventHandler<EventArgs> MarkIn;
        public event EventHandler<EventArgs> MarkOut;

        public event EventHandler<RepeatMode> RepeatToggled;

        private Canvas ChapterMarkersCanvas;
        private Slider ProgressSlider;
        private Popup TimePreviewPopup;
        private Border TimePreviewBorder;
        private TextBlock TimePreviewText;
        private List<ChapterInfo> Chapters = new List<ChapterInfo>();
        private TimeSpan MediaDuration;

        private Rectangle MarkInMarker;
        private Rectangle MarkOutMarker;
        private TimeSpan? MarkInTime;
        private TimeSpan? MarkOutTime;

        public CustomMediaTransportControls()
        {
            this.DefaultStyleKey = typeof(CustomMediaTransportControls);
        }

        protected override void OnApplyTemplate()
        {
            // This is where you would get your custom button and create an event handler for its click method.
            Button likeButton = GetTemplateChild("LikeButton") as Button;
            likeButton.Click += LikeButton_Click;

            Button MarkInButton = GetTemplateChild("MarkInButton") as Button;
            MarkInButton.Click += MarkInButton_Click;

            Button MarkOutButton = GetTemplateChild("MarkOutButton") as Button;
            MarkOutButton.Click += MarkOutButton_Click;

            AppBarButton repeatButton = GetTemplateChild("RepeatButton") as AppBarButton;
            repeatButton.Click += RepeatButton_Click;
            SetRepeatMode(repeatButton, false);

            ChapterMarkersCanvas = GetTemplateChild("ChapterMarkersCanvas") as Canvas;
            ProgressSlider = GetTemplateChild("ProgressSlider") as Slider;

            if (ProgressSlider != null)
            {
                ProgressSlider.SizeChanged += OnProgressSliderSizeChanged;

                // Create custom time preview popup
                CreateTimePreviewPopup();

                ProgressSlider.PointerEntered += OnProgressSliderPointerEntered;
                ProgressSlider.PointerExited += OnProgressSliderPointerExited;
                ProgressSlider.PointerMoved += OnProgressSliderPointerMoved;
            }

            base.OnApplyTemplate();
        }



        private void CreateTimePreviewPopup()
        {
            // Create styled text block for time display
            TimePreviewText = new TextBlock
            {
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Create border with rounded corners and shadow
            TimePreviewBorder = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(220, 32, 32, 32)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Child = TimePreviewText,
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            // Add shadow effect
            TimePreviewBorder.Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
            TimePreviewBorder.Translation = new System.Numerics.Vector3(0, 0, 32);

            // Create popup
            TimePreviewPopup = new Popup
            {
                Child = TimePreviewBorder,
                IsLightDismissEnabled = false,
                IsOpen = false
            };
        }

        private void OnProgressSliderPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TimePreviewPopup != null && MediaDuration.TotalSeconds > 0)
            {
                // Set XamlRoot from the slider (required for WinUI 3)
                if (TimePreviewPopup.XamlRoot == null && ProgressSlider?.XamlRoot != null)
                {
                    TimePreviewPopup.XamlRoot = ProgressSlider.XamlRoot;
                }

                TimePreviewPopup.IsOpen = true;
            }
        }

        private void OnProgressSliderPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TimePreviewPopup != null)
            {
                TimePreviewPopup.IsOpen = false;
            }
        }

        private void OnProgressSliderPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ProgressSlider == null || TimePreviewPopup == null || MediaDuration.TotalSeconds <= 0)
                return;

            var position = e.GetCurrentPoint(ProgressSlider).Position;
            var sliderWidth = ProgressSlider.ActualWidth;

            if (sliderWidth > 0 && position.X >= 0 && position.X <= sliderWidth)
            {
                double percentage = position.X / sliderWidth;
                TimeSpan hoveredTime = TimeSpan.FromSeconds(MediaDuration.TotalSeconds * percentage);

                UpdateTimePreview(hoveredTime, position.X);
            }
        }

        private void UpdateTimePreview(TimeSpan time, double xPosition)
        {
            if (TimePreviewText == null || TimePreviewPopup == null)
                return;

            // Format time and chapter info
            string timestamp = FormatTimeSpan(time);
            string chapterTitle = GetChapterAtTime(time);

            if (!string.IsNullOrEmpty(chapterTitle))
            {
                TimePreviewText.Text = $"{timestamp}\n{chapterTitle}";
            }
            else
            {
                TimePreviewText.Text = timestamp;
            }

            // Position popup above the cursor
            TimePreviewBorder.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            double popupWidth = TimePreviewBorder.DesiredSize.Width;
            double popupHeight = TimePreviewBorder.DesiredSize.Height;

            // Get slider position relative to window
            var sliderTransform = ProgressSlider.TransformToVisual(null);
            var sliderPoint = sliderTransform.TransformPoint(new Windows.Foundation.Point(0, 0));

            // Center popup horizontally on cursor, position above slider
            TimePreviewPopup.HorizontalOffset = sliderPoint.X + xPosition - (popupWidth / 2);
            TimePreviewPopup.VerticalOffset = sliderPoint.Y - popupHeight - 10;
        }

        private string GetChapterAtTime(TimeSpan time)
        {
            if (Chapters == null || Chapters.Count == 0)
                return string.Empty;

            ChapterInfo currentChapter = null;

            foreach (var chapter in Chapters.OrderBy(c => c.StartTime))
            {
                if (time >= chapter.StartTime)
                {
                    currentChapter = chapter;
                }
                else
                {
                    break;
                }
            }

            return currentChapter?.Title ?? string.Empty;
        }

        private string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"h\:mm\:ss");
            }
            else
            {
                return time.ToString(@"m\:ss");
            }
        }

        // Public methods to set Mark In/Out from external code
        public void SetMarkIn(TimeSpan time)
        {
            MarkInTime = time;
            UpdateMarkInOutMarkers();
        }

        public void SetMarkOut(TimeSpan time)
        {
            MarkOutTime = time;
            UpdateMarkInOutMarkers();
        }

        public void ClearMarkIn()
        {
            MarkInTime = null;
            UpdateMarkInOutMarkers();
        }

        public void ClearMarkOut()
        {
            MarkOutTime = null;
            UpdateMarkInOutMarkers();
        }

        public void ClearMarks()
        {
            MarkInTime = null;
            MarkOutTime = null;
            UpdateMarkInOutMarkers();
        }

        // Call this to set the media duration from FFmpeg
        public void SetMediaDuration(TimeSpan duration)
        {
            MediaDuration = duration;
            UpdateChapterMarkers();
        }

        // Call this after loading chapters from FFmpeg
        public void LoadChapters(List<ChapterInfo> chapters)
        {
            Chapters = chapters;
            UpdateChapterMarkers();
        }

        // Overload to load both chapters and duration at once
        public void LoadMediaInfo(List<ChapterInfo> chapters, TimeSpan duration)
        {
            Chapters = chapters;
            MediaDuration = duration;
            UpdateChapterMarkers();
        }

        private void OnProgressSliderSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChapterMarkers();
        }

        private void UpdateChapterMarkers()
        {
            if (ChapterMarkersCanvas == null || ProgressSlider == null)
                return;

            double totalDuration = MediaDuration.TotalSeconds;

            if (totalDuration <= 0)
                return;

            ChapterMarkersCanvas.Children.Clear();

            double sliderWidth = ProgressSlider.ActualWidth;

            // Add chapter markers
            if (Chapters != null && Chapters.Count > 0)
            {
                foreach (var chapter in Chapters)
                {
                    double position = (chapter.StartTime.TotalSeconds / totalDuration) * sliderWidth;

                    // Create chapter marker
                    var marker = new Rectangle
                    {
                        Width = 2,
                        Height = 8,
                        Fill = new SolidColorBrush(Colors.Gray),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    Canvas.SetLeft(marker, position);
                    Canvas.SetTop(marker, 12.5); // Center vertically

                    ChapterMarkersCanvas.Children.Add(marker);
                }
            }

            // Update Mark In/Out markers
            UpdateMarkInOutMarkers();
        }

        private void UpdateMarkInOutMarkers()
        {
            if (ChapterMarkersCanvas == null || ProgressSlider == null)
                return;

            double totalDuration = MediaDuration.TotalSeconds;

            if (totalDuration <= 0)
                return;

            double sliderWidth = ProgressSlider.ActualWidth;

            // Remove existing Mark In marker if it exists
            if (MarkInMarker != null && ChapterMarkersCanvas.Children.Contains(MarkInMarker))
            {
                ChapterMarkersCanvas.Children.Remove(MarkInMarker);
            }

            // Remove existing Mark Out marker if it exists
            if (MarkOutMarker != null && ChapterMarkersCanvas.Children.Contains(MarkOutMarker))
            {
                ChapterMarkersCanvas.Children.Remove(MarkOutMarker);
            }

            // Add Mark In marker
            if (MarkInTime.HasValue)
            {
                double position = (MarkInTime.Value.TotalSeconds / totalDuration) * sliderWidth;

                MarkInMarker = new Rectangle
                {
                    Width = 3,
                    Height = 12,
                    Fill = new SolidColorBrush(Colors.Red),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Canvas.SetLeft(MarkInMarker, position);
                Canvas.SetTop(MarkInMarker, 10.5); // Center vertically

                ToolTipService.SetToolTip(MarkInMarker, $"Mark In: {FormatTimeSpan(MarkInTime.Value)}");

                ChapterMarkersCanvas.Children.Add(MarkInMarker);
            }

            // Add Mark Out marker
            if (MarkOutTime.HasValue)
            {
                double position = (MarkOutTime.Value.TotalSeconds / totalDuration) * sliderWidth;

                MarkOutMarker = new Rectangle
                {
                    Width = 3,
                    Height = 12,
                    Fill = new SolidColorBrush(Colors.Red),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Canvas.SetLeft(MarkOutMarker, position);
                Canvas.SetTop(MarkOutMarker, 10.5); // Center vertically

                ToolTipService.SetToolTip(MarkOutMarker, $"Mark Out: {FormatTimeSpan(MarkOutTime.Value)}");

                ChapterMarkersCanvas.Children.Add(MarkOutMarker);
            }
        }

        private void MarkOutButton_Click(object sender, RoutedEventArgs e)
        {
            MarkOut?.Invoke(this, EventArgs.Empty);
        }

        private void MarkInButton_Click(object sender, RoutedEventArgs e)
        {
            MarkIn?.Invoke(this, EventArgs.Empty);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var RepeatButton = sender as AppBarButton;
            var currentMode = Settings.Current.RepeatMode;

            // Cycle through the three modes and save to settings
            Settings.Current.RepeatMode = currentMode switch
            {
                RepeatMode.Off => RepeatMode.RepeatOne,
                RepeatMode.RepeatOne => RepeatMode.RepeatAll,
                RepeatMode.RepeatAll => RepeatMode.Off,
                _ => RepeatMode.Off
            };

            SetRepeatMode(RepeatButton, true);
        }

        public void SetRepeatMode(AppBarButton Button, bool Invoke)
        {
            var mode = Settings.Current.RepeatMode;

            Button.Icon = mode switch
            {
                RepeatMode.RepeatOne => new FontIcon() { Glyph = "\uE8ED" }, // Repeat One Icon
                RepeatMode.RepeatAll => new FontIcon() { Glyph = "\uE8EE" }, // Repeat All Icon
                _ => new FontIcon() { Glyph = "\uF5E7" } // Repeat Off Icon
            };

            if (Invoke) RepeatToggled?.Invoke(Button, mode);
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise an event on the custom control when 'like' is clicked
            Liked?.Invoke(this, EventArgs.Empty);
        }
    }




}