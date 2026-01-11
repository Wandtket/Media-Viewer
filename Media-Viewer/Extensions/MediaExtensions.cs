using FFMpegCore;
using MediaViewer.Controls.Dialogs;
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
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI.ViewManagement;

namespace MediaViewer.Extensions
{


    public static class MediaExtensions
    {


        /// <summary>
        /// Opens media in user specified applications.
        /// </summary>
        /// <param name="File"></param>
        /// <param name="action"></param>
        public static async void OpenFile(StorageFile File, OpenAction action = OpenAction.Editor)
        {
            var Type = Files.GetMediaType(File.Path);

            LauncherOptions options = new LauncherOptions
            {
                DisplayApplicationPicker = true,
            };

            if (Type == MediaType.Image)
            {
                if (!string.IsNullOrEmpty(Settings.Current.ImageEditorPath) && action == OpenAction.Editor)
                {
                    Process.Start(Settings.Current.ImageEditorPath, File.Path);
                }
                else if (!string.IsNullOrEmpty(Settings.Current.ImageUpscalerPath) && action == OpenAction.Upscaler)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Settings.Current.ImageUpscalerPath,
                        Arguments = $"\"{File.Path}\"",
                        UseShellExecute = true, // Show the GUI
                        CreateNoWindow = false
                    };

                    var process = new Process { StartInfo = psi };
                    process.Start();
                }
                else { await Launcher.LaunchFileAsync(File, options); }
            }
            else if (Type == MediaType.Gif)
            {
                if (!string.IsNullOrEmpty(Settings.Current.GifEditorPath))
                {
                    Process.Start(Settings.Current.GifEditorPath, File.Path);
                }
                else { await Launcher.LaunchFileAsync(File, options); }
            }
            else if (Type == MediaType.Audio)
            {
                if (!string.IsNullOrEmpty(Settings.Current.AudioEditorPath))
                {
                    Process.Start(Settings.Current.AudioEditorPath, File.Path);
                }
                else { await Launcher.LaunchFileAsync(File, options); }
            }
            else if (Type == MediaType.Video)
            {
                if (!string.IsNullOrEmpty(Settings.Current.VideoEditorPath) && action == OpenAction.Editor)
                {
                    //If the user decides to open with premiere a new project is created and the video is imported via jsx
                    var editorName = System.IO.Path.GetFileName(Settings.Current.VideoEditorPath ?? "").ToLowerInvariant();
                    if (editorName.Contains("premiere"))
                    {
                        await OpenInPremiereWithImportAsync(File);
                    }
                    else
                    {
                        Process.Start(Settings.Current.VideoEditorPath, File.Path);
                    }
                }
                else if (!string.IsNullOrEmpty(Settings.Current.VideoUpscalerPath) && action == OpenAction.Upscaler)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Settings.Current.VideoUpscalerPath,
                        Arguments = $"\"{File.Path}\"",
                        UseShellExecute = true, // Show the GUI
                        CreateNoWindow = false
                    };

                    var process = new Process { StartInfo = psi };
                    process.Start();
                }
                else if (!string.IsNullOrEmpty(Settings.Current.VideoConverterPath) && action == OpenAction.Converter)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Settings.Current.VideoConverterPath,
                        Arguments = $"\"{File.Path}\"",
                        UseShellExecute = true, // Show the GUI
                        CreateNoWindow = false
                    };

                    var process = new Process { StartInfo = psi };
                    process.Start();
                }
                else { await Launcher.LaunchFileAsync(File, options); }
            }
        }


        /// <summary>
        /// Extracts a single frame from a video file using FFmpeg.
        /// </summary>
        public static async Task ExtractVideoFrameAsync(string inputPath, TimeSpan position, string outputPath, bool isHDR)
        {
            string filterChain = isHDR
                ? "zscale=t=linear:npl=100,zscale=t=bt709:m=bt709:r=tv,scale=in_range=tv:out_range=pc,format=rgb24"
                : "scale=in_range=tv:out_range=pc,format=rgb24";

            await FFMpegArguments
                .FromFileInput(inputPath, true, options => options.Seek(position))
                .OutputToFile(outputPath, overwrite: true, options => options
                    .WithCustomArgument($"-vf \"{filterChain}\"")
                    .WithCustomArgument("-frames:v 1")
                    .WithCustomArgument("-q:v 2"))
                .ProcessAsynchronously();
        }

        /// <summary>
        /// Detects whether the video is HDR based on its media information.
        /// </summary>
        /// <param name="mediaInfo"></param>
        /// <returns></returns>
        public static bool IsHDRVideo(FFMpegCore.IMediaAnalysis mediaInfo)
        {
            var videoStream = mediaInfo.PrimaryVideoStream;
            if (videoStream == null) return false;

            // Check for HDR indicators: 10-bit depth, PQ/HLG transfer, or wide color gamut
            bool is10Bit = videoStream.BitsPerRawSample >= 10 || videoStream.PixelFormat?.Contains("10") == true;
            string colorTransfer = videoStream.ColorTransfer?.ToLower() ?? "";
            string colorSpace = videoStream.ColorSpace?.ToLower() ?? "";

            bool hasHDRTransfer = colorTransfer.Contains("smpte2084") || colorTransfer.Contains("arib-std-b67") || colorTransfer.Contains("bt2020");
            bool hasWideGamut = colorSpace.Contains("bt2020");

            return is10Bit && (hasHDRTransfer || hasWideGamut);
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


        /// <summary>
        /// Gets the video duration using FFProbe.
        /// </summary>
        /// <param name="videoPath"></param>
        /// <returns></returns>
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


        /// <summary>
        /// Converts a Gif to a static image if the user selects it when saving.
        /// </summary>
        /// <param name="gifFile"></param>
        /// <param name="destFile"></param>
        /// <param name="encoderId"></param>
        /// <returns></returns>
        public static async Task ConvertGifToImageAsync(this StorageFile gifFile, StorageFile destFile, Guid encoderId)
        {
            using (var stream = await gifFile.OpenAsync(FileAccessMode.Read))
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var frame = await decoder.GetFrameAsync(0);

                using (var outStream = await destFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(encoderId, outStream);
                    var pixelData = await frame.GetPixelDataAsync(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
                        new Windows.Graphics.Imaging.BitmapTransform(),
                        Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
                        Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
                        decoder.OrientedPixelWidth,
                        decoder.OrientedPixelHeight,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixelData.DetachPixelData());

                    await encoder.FlushAsync();
                }
            }
        }


        /// <summary>
        /// Opens a video file in Premiere Pro using a template project and import the file.
        /// </summary>
        public static async Task OpenInPremiereWithImportAsync(StorageFile videoFile)
        {
            try
            {
                // Check extension against a list of formats commonly supported by Adobe Premiere
                var ext = System.IO.Path.GetExtension(videoFile.Path)?.ToLowerInvariant() ?? string.Empty;
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".mp4", ".mov", ".m4v", ".mxf", ".avi", ".wmv", ".mpg", ".mpeg", ".flv", ".mts", ".m2ts"
                };

                if (!supportedExtensions.Contains(ext))
                {
                    await MessageBox.Show($"Adobe Premiere does not support files with the '{ext}' extension.\n\n" +
                        $"File: {videoFile.Name}\n\nPlease convert the file to a supported format.",
                        "Unsupported Format");
                    return;
                }

                var LocalPath = ApplicationData.Current.LocalFolder.Path;

                var PrprojTemplate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "Template.prproj");
                var JsxTemplate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "Template.jsx");

                var PrprojPath = System.IO.Path.Combine(LocalPath, $"{videoFile.DisplayName}.prproj");
                var JsxPath = System.IO.Path.Combine(LocalPath, $"Template.jsx");

                File.Copy(PrprojTemplate, PrprojPath, true);
                File.Copy(JsxTemplate, JsxPath, true);


                string JSX = File.ReadAllText(JsxPath)
                    .Replace("{PRPROJ}", PrprojPath)
                    .Replace("{FILEPATH}", videoFile.Path)
                    .Replace("\\", "\\\\");
                File.WriteAllText(JsxPath, JSX);


                // Pass only the JSX via -s; the JSX itself will open the project file.
                var args = $" /C es.processFile \"{JsxPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = Settings.Current.VideoEditorPath,
                    Arguments = args,
                    UseShellExecute = true,

                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex);
            }
        }




        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;


        public static async void SetAsDesktopBackgroundAsync(this StorageFile file)
        {
            var type = Files.GetMediaType(file.Path);

            if (type == MediaType.Image || type == MediaType.Gif)
            {
                // Only works for BMP/JPG/PNG files
                bool result = SystemParametersInfo(
                    SPI_SETDESKWALLPAPER, 0, file.Path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

                if (!result)
                {
                    await ErrorBox.Show(new Exception("Failed to set desktop background."));
                }
            }


            var confirm = await ConfirmBox.Show("Would you like to open settings for further customization?", "Background Set", "Yes", "No");
            if (confirm == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:personalization-background"));
            }
        }

        public static async void SetAsLockScreenAsync(this StorageFile File)
        {
            var Type = Files.GetMediaType(File.Path);

            if (Type == MediaType.Image)
            {
                try
                {
                    // Copy to LocalFolder (overwrite if exists)
                    var localFolder = ApplicationData.Current.LocalFolder;
                    var copiedFile = await File.CopyAsync(localFolder, File.Name, NameCollisionOption.ReplaceExisting);

                    // Try to set as lock screen
                    bool result = await UserProfilePersonalizationSettings.Current.TrySetLockScreenImageAsync(copiedFile);
                    await copiedFile.DeleteAsync();
                }
                catch
                {
                    await ErrorBox.Show(new Exception("Failed to set lock screen image."));
                }
            }

            var confirm = await ConfirmBox.Show("Would you like to open settings for further customization?", "Lockscreen Set", "Yes", "No");
            if (confirm == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:lockscreen"));
            }
        }

    }

    public sealed class CustomMediaTransportControls : MediaTransportControls
    {
        public event EventHandler<EventArgs> Liked;

        public event EventHandler<EventArgs> MarkIn;
        public event EventHandler<EventArgs> MarkOut;
        public event EventHandler<EventArgs> MarksCleared;

        public event EventHandler<RepeatMode> RepeatToggled;

        private Canvas ChapterMarkersCanvas;
        private Slider ProgressSlider;
        private Popup TimePreviewPopup;
        private Border TimePreviewBorder;
        private TextBlock TimePreviewText;
        private List<ChapterInfo> Chapters = new List<ChapterInfo>();
        private TimeSpan MediaDuration;

        private UIElement MarkInMarker;
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

            Button ClearMarksButton = GetTemplateChild("ClearMarksButton") as Button;
            ClearMarksButton.Click += async (_, __) => ClearMarks();

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
            MarksCleared?.Invoke(this, EventArgs.Empty); // Notify that marks were cleared
        }

        // Call this to set the media duration from FFmpeg
        public void SetMediaDuration(TimeSpan duration)
        {
            MediaDuration = duration;
            UpdateChapterMarkers();
            UpdateMarkInOutMarkers();
        }

        // Call this after loading chapters from FFmpeg
        public void LoadChapters(List<ChapterInfo> chapters)
        {
            Chapters = chapters;
            UpdateChapterMarkers();
            UpdateMarkInOutMarkers();
        }

        // Overload to load both chapters and duration at once
        public void LoadMediaInfo(List<ChapterInfo> chapters, TimeSpan duration)
        {
            Chapters = chapters;
            MediaDuration = duration;
            UpdateChapterMarkers();
            UpdateMarkInOutMarkers();
        }

        private void OnProgressSliderSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChapterMarkers();
            UpdateMarkInOutMarkers();
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
        }

        private void UpdateMarkInOutMarkers()
        {
            if (ChapterMarkersCanvas == null || ProgressSlider == null)
                return;

            double totalDuration = MediaDuration.TotalSeconds;
            if (totalDuration <= 0)
                return;

            double sliderWidth = ProgressSlider.ActualWidth;

            // Default WinUI Slider thumb width is 20px; adjust if your style is different
            double thumbWidth = 20.0;
            double thumbOffset = thumbWidth / 2.0;

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
            
            // Also remove any Grid-based markers tagged as mark indicators
            var markersToRemove = ChapterMarkersCanvas.Children.OfType<Grid>()
                .Where(g => g.Tag?.ToString() == "MarkOutMarker" || g.Tag?.ToString() == "MarkInMarker")
                .ToList();
            foreach (var marker in markersToRemove)
            {
                ChapterMarkersCanvas.Children.Remove(marker);
            }

            // Remove any existing mark region highlighting
            var existingRegion = ChapterMarkersCanvas.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name == "MarkRegionHighlight");
            if (existingRegion != null)
            {
                ChapterMarkersCanvas.Children.Remove(existingRegion);
            }

            // Add highlighted region between Mark In and Mark Out
            if (MarkInTime.HasValue && MarkOutTime.HasValue)
            {
                double markInPos = (MarkInTime.Value.TotalSeconds / totalDuration) * sliderWidth + thumbOffset;
                double markOutPos = (MarkOutTime.Value.TotalSeconds / totalDuration) * sliderWidth + thumbOffset;
                double regionWidth = markOutPos - markInPos;

                if (regionWidth > 0)
                {
                    var regionHighlight = new Rectangle
                    {
                        Name = "MarkRegionHighlight",
                        Width = regionWidth,
                        Height = 6,
                        Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(60, 255, 193, 7)), // Semi-transparent amber
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    Canvas.SetLeft(regionHighlight, markInPos);
                    Canvas.SetTop(regionHighlight, 13.5); // Center with the slider track

                    ChapterMarkersCanvas.Children.Insert(0, regionHighlight); // Add behind markers

                    // Calculate duration between marks
                    TimeSpan duration = MarkOutTime.Value - MarkInTime.Value;
                    ToolTipService.SetToolTip(regionHighlight, 
                        $"Marked Section: {FormatTimeSpan(duration)}\n" +
                        $"From: {FormatTimeSpan(MarkInTime.Value)}\n" +
                        $"To: {FormatTimeSpan(MarkOutTime.Value)}");
                }
            }

            // Add Mark In marker with enhanced visibility
            if (MarkInTime.HasValue)
            {
                double position = (MarkInTime.Value.TotalSeconds / totalDuration) * sliderWidth;
                position += thumbOffset;

                // Create a composite marker with a flag shape
                var markInContainer = new Grid
                {
                    Width = 20,
                    Height = 40,
                    Tag = "MarkInMarker"
                };

                // Vertical line
                var line = new Rectangle
                {
                    Width = 3,
                    Height = 40,
                    Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 76, 175, 80)), // Bright green
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // Flag/triangle at top for "In"
                var flag = new Polygon
                {
                    Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 76, 175, 80)),
                    Points = new PointCollection
                    {
                        new Windows.Foundation.Point(2, 0),
                        new Windows.Foundation.Point(2, 10),
                        new Windows.Foundation.Point(12, 5)
                    },
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                markInContainer.Children.Add(line);
                markInContainer.Children.Add(flag);

                MarkInMarker = markInContainer;

                Canvas.SetLeft(MarkInMarker, position - 10); // Center the 20px wide marker
                Canvas.SetTop(MarkInMarker, 0);

                ToolTipService.SetToolTip(MarkInMarker, 
                    $"Mark In: {FormatTimeSpan(MarkInTime.Value)}\n" +
                    $"Press [ to set/clear");

                ChapterMarkersCanvas.Children.Add(MarkInMarker);
            }

            // Add Mark Out marker with enhanced visibility
            if (MarkOutTime.HasValue)
            {
                double position = (MarkOutTime.Value.TotalSeconds / totalDuration) * sliderWidth;
                position += thumbOffset;

                // Create a composite marker with a flag shape
                var markOutContainer = new Grid
                {
                    Width = 20,
                    Height = 40,
                    Tag = "MarkOutMarker"
                };

                // Vertical line
                var line = new Rectangle
                {
                    Width = 3,
                    Height = 40,
                    Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 67, 54)), // Bright red
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // Flag/triangle at top for "Out"
                var flag = new Polygon
                {
                    Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 67, 54)),
                    Points = new PointCollection
                    {
                        new Windows.Foundation.Point(18, 0),
                        new Windows.Foundation.Point(18, 10),
                        new Windows.Foundation.Point(8, 5)
                    },
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                markOutContainer.Children.Add(line);
                markOutContainer.Children.Add(flag);

                Canvas.SetLeft(markOutContainer, position - 10); // Center the 20px wide marker
                Canvas.SetTop(markOutContainer, 0);

                ToolTipService.SetToolTip(markOutContainer, 
                    $"Mark Out: {FormatTimeSpan(MarkOutTime.Value)}\n" +
                    $"Press ] to set/clear");

                ChapterMarkersCanvas.Children.Add(markOutContainer);
                
                // Keep reference for compatibility with existing code
                MarkOutMarker = line;
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


