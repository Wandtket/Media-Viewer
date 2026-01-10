using CommunityToolkit.WinUI;
using FFMpegCore;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using MediaViewer.Models;
using MediaViewer.Pages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI.Input.Preview.Injection;
using WinRT.Interop;
using Pointer = Microsoft.UI.Xaml.Input.Pointer;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Controls;



public sealed partial class VisualPlayer : UserControl
{

    public MediaPage? ParentPage;

    public StorageFile CurrentFile;
    public MediaProperties Properties;

    public BitmapImage Thumbnail = new();
    private BitmapImage GifImage;


    public bool MediaLoaded = false;

    private bool isPaused = false;
    public bool CanRotate = true;
    public bool isHDR = false;

    public bool isFreeMove = false;
    private bool _isSeamlessLooping = false;
    private bool _initialMediaOpened = false;


    public VisualPlayer()
    {
        InitializeComponent();

        this.SizeChanged += VisualPlayer_SizeChanged;
        this.Unloaded += VisualPlayer_Unloaded;
        App.Current.Player = this;
    }

    private void VisualPlayer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ImageElement.Visibility == Visibility.Visible)
        {
            ImageElement.Height = this.ActualHeight;
        }
        else
        {
            if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter)
            {
                presenter.Height = this.ActualHeight;
            }
        }
    }

    private void VisualPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        // Save position when control is unloaded
        SavePlaybackState();
    }



    public void SetSource(StorageFile source)
    {
        CurrentFile = source;
        Properties = new MediaProperties(source);

        if (Files.GetMediaType(source.Path) == MediaType.Image)
        {
            LoadImage(source);
        }
        else if (Files.GetMediaType(source.Path) == MediaType.Gif)
        {
            LoadGIF(source);
        }
        else if (Files.GetMediaType(source.Path) == MediaType.Video)
        {
            LoadVideo(source);
        }

        MediaLoaded = true;
    }

    private async void LoadImage(StorageFile source)
    {
        VideoElement.Visibility = Visibility.Collapsed;
        ImageElement.Visibility = Visibility.Visible;

        var bitmap = new BitmapImage();
        using (var stream = await source.OpenReadAsync())
        {
            await bitmap.SetSourceAsync(stream);
        }
        ImageElement.Source = bitmap;
        LoadRing.IsActive = false;
        CanRotate = true;

        await Task.Delay(100);
        FitContentToWidth();
    }

    private async void LoadGIF(StorageFile source)
    {
        VideoElement.Visibility = Visibility.Collapsed;
        ImageElement.Visibility = Visibility.Visible;

        GifImage = new BitmapImage();
        using (var stream = await source.OpenReadAsync())
        {
            await GifImage.SetSourceAsync(stream);
        }

        ImageElement.Source = GifImage;
        LoadRing.IsActive = false;
        CanRotate = false;

        ParentPage?.ParentPage?.RotateButton.IsEnabled = false;

        await Task.Delay(100);
        FitContentToWidth();
    }

    private async void LoadVideo(StorageFile source)
    {
        ImageElement.Visibility = Visibility.Collapsed;
        ImageElement.Source = null;

        VideoElement.Visibility = Visibility.Visible;

        Windows.Media.Playback.MediaPlayer player = new Windows.Media.Playback.MediaPlayer();
        
        // For network drives and large files: Disable RealTimePlayback to allow more buffering
        // This prevents stuttering when loading from network locations
        player.RealTimePlayback = false;
        
        // Enable audio/video frame server mode for better performance with high-bitrate content
        // This reduces latency and improves responsiveness
        player.AudioCategory = Windows.Media.Playback.MediaPlayerAudioCategory.Movie;
        player.AudioDeviceType = Windows.Media.Playback.MediaPlayerAudioDeviceType.Multimedia;
        
        // For seamless looping (especially for short videos), use MediaPlaybackList
        // This enables gapless playback and eliminates stuttering at loop points
        if (Settings.Current.RepeatMode == RepeatMode.RepeatOne)
        {
            _isSeamlessLooping = true;
            
            // Create MediaSource from file
            var mediaSource = MediaSource.CreateFromStorageFile(source);
            
            // Create MediaPlaybackItem
            var playbackItem = new MediaPlaybackItem(mediaSource);
            
            // Create MediaPlaybackList for seamless looping
            var playbackList = new MediaPlaybackList();
            
            // Enable gapless playback - this is key for seamless loops
            playbackList.AutoRepeatEnabled = true;
            playbackList.MaxPrefetchTime = TimeSpan.FromSeconds(5); // Pre-buffer up to 5 seconds
            
            // Add the same item to the list
            playbackList.Items.Add(playbackItem);
            
            // Set the source to the playback list
            player.Source = playbackList;
        }
        else
        {
            _isSeamlessLooping = false;
            
            // For non-looping playback, use regular MediaSource
            player.IsLoopingEnabled = false;
            player.SetUriSource(new Uri(source.Path));
        }

        VideoElement.SetMediaPlayer(player);
        VideoElement.AreTransportControlsEnabled = true;
        VideoElement.AllowFocusOnInteraction = true;
        VideoElement.IsTapEnabled = true;

        VideoElement.MediaPlayer.MediaOpened += VisualPlayer_MediaOpened;
        VideoElement.MediaPlayer.PlaybackSession.PlaybackStateChanged += VisualPlayer_PlaybackStateChanged;
        VideoElement.MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        VideoElement.MediaPlayer.MediaEnded += VisualPlayer_MediaEnded;

        LoadRing.IsActive = false;
        CanRotate = false;

        ParentPage?.ParentPage?.RotateButton.IsEnabled = false;

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(source.Path);
            VideoFrameRate = mediaInfo.PrimaryVideoStream?.FrameRate ?? 30.0;
            isHDR = MediaExtensions.IsHDRVideo(mediaInfo);
        }
        catch
        {
            // Ignore ffprobe errors if video can still be played
            VideoFrameRate = 30.0;
        }

        try
        {
            var duration = VideoElement.MediaPlayer.PlaybackSession.NaturalDuration;
            ((CustomMediaTransportControls)VideoElement.TransportControls).SetMediaDuration(duration);

            var chapters = await MediaExtensions.ExtractChaptersAsync(source.Path);
            if (chapters.Count > 0)
            {
                ((CustomMediaTransportControls)VideoElement.TransportControls).LoadChapters(chapters);
            }
        }
        catch
        {
            // Ignore chapter extraction errors
        }
    }


    public async Task Rotate()
    {      
        if (CanRotate)
        {
            ImageRotation.Angle = (ImageRotation.Angle + 90) % 360;
            await ImageElement.Rotate(CurrentFile, Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees);
            FitContentToWidth();
        }
    }


    public async void BeginMediaPreview(DeviceInformation Device)
    {
        if (AppCapability.Create("Webcam").CheckAccess() != AppCapabilityAccessStatus.Allowed)
        {
            //tbStatus.Text = "Camera access denied. Launching settings.";

            bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-webcam"));

            if (AppCapability.Create("Webcam").CheckAccess() != AppCapabilityAccessStatus.Allowed)
            {
                //tbStatus.Text = "Camera access denied in privacy settings.";
                return;
            }
        }

        try
        {
            var mediaCapture = new MediaCapture();
            var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
            {
                VideoDeviceId = Device.Id,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo,
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
            };

            await mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

            MediaFrameSource m_frameSource = null;

            // Find preview source.
            // The preferred preview stream from a camera is defined by MediaStreamType.VideoPreview on the RGB camera (SourceKind == color).
            var previewSource = mediaCapture.FrameSources.FirstOrDefault(source => source.Value.Info.MediaStreamType == MediaStreamType.VideoPreview
                                                                                        && source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;

            if (previewSource != null)
            {
                m_frameSource = previewSource;
            }
            else
            {
                var recordSource = mediaCapture.FrameSources.FirstOrDefault(source => source.Value.Info.MediaStreamType == MediaStreamType.VideoRecord
                                                                                           && source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;
                if (recordSource != null)
                {
                    m_frameSource = recordSource;
                }
            }

            if (m_frameSource == null)
            {
                await MessageBox.Show("No video preview or record stream found.");
                return;
            }



            // Create VisualPlayer with the preview source
            Windows.Media.Playback.MediaPlayer player = new Windows.Media.Playback.MediaPlayer();
            //player.RealTimePlayback = true;
            //player.AutoPlay = true;
            //player.IsLoopingEnabled = true;

            player.Source = MediaSource.CreateFromMediaFrameSource(m_frameSource);
            //m_VisualPlayer.MediaFailed += VisualPlayer_MediaFailed; ;

            // Set the VisualPlayer on the VisualPlayerElement
            VideoElement.SetMediaPlayer(player);
            VideoElement.AreTransportControlsEnabled = false;
            VideoElement.AllowFocusOnInteraction = false;
            VideoElement.IsFocusEngagementEnabled = false;
            VideoElement.IsTapEnabled = false;

            // Start preview
            player.Play();

            //m_isPreviewing = true;
            //bStartPreview.IsEnabled = false;
            //bStopPreview.IsEnabled = true;

            //tbStatus.Text = "MediaCapture initialized successfully.";
        }
        catch (Exception ex)
        {
            await ErrorBox.Show(ex, Title: "Initialize media capture failed");
        }
    }



    private void VisualPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
    {
        var dispatcherQueue = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;

        dispatcherQueue.TryEnqueue(delegate
        {
            if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter)
            {
                presenter.Height = this.Height;
                presenter.Height = this.ActualHeight;
                LoadRing.IsActive = false;
            }

            // For seamless looping, prevent transport controls from showing on loop restarts
            // Only allow restore on the first MediaOpened event
            if (_isSeamlessLooping && _initialMediaOpened)
            {
                // This is a loop restart - don't restore state or show controls
                // The transport controls auto-hide timer should continue
                return;
            }

            _initialMediaOpened = true;

            var savedState = LoadPlaybackState(CurrentFile);

            // Restore state after media is opened
            if (savedState != null)
            {
                var dispatcherQueue = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;
                dispatcherQueue.TryEnqueue(() =>
                {
                    // Restore position
                    if (savedState.Position > TimeSpan.Zero)
                    {
                        sender.PlaybackSession.Position = savedState.Position;
                    }

                    // Restore volume
                    sender.Volume = savedState.Volume;

                    // Restore mark in/out
                    if (savedState.MarkIn.HasValue)
                    {
                        _markInPosition = savedState.MarkIn;
                        ((CustomMediaTransportControls)VideoElement.TransportControls).SetMarkIn(savedState.MarkIn.Value);
                    }

                    if (savedState.MarkOut.HasValue)
                    {
                        _markOutPosition = savedState.MarkOut;
                        ((CustomMediaTransportControls)VideoElement.TransportControls).SetMarkOut(savedState.MarkOut.Value);
                    }

                    // Enable looping if both marks are restored
                    if (_markInPosition.HasValue && _markOutPosition.HasValue)
                    {
                        EnableMarkedSectionLooping();
                    }
                });
            }
        });
    }

    private void VisualPlayer_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
    {
        var dispatcherQueue = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;

        dispatcherQueue.TryEnqueue(() =>
        {
            isPaused = sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused;
        });
    }

    private void VisualPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
    {
        var dispatcherQueue = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;
        dispatcherQueue.TryEnqueue(() =>
        {
            if (_markInPosition.HasValue && !_markOutPosition.HasValue)
            {
                // Only Mark In is set: loop to Mark In
                sender.PlaybackSession.Position = _markInPosition.Value;
                sender.Play();
            }
            else if (_markOutPosition.HasValue && !_markInPosition.HasValue)
            {
                // Only Mark Out is set: loop to beginning
                sender.PlaybackSession.Position = TimeSpan.Zero;
                sender.Play();
            }
            else if (Settings.Current.RepeatMode == RepeatMode.RepeatAll)
            {
                AdvanceToNextItem();
            }
        });
    }


    private void AdvanceToNextItem()
    {
        if (ParentPage?.MediaFlipView == null) return;

        int currentIndex = ParentPage.MediaFlipView.SelectedIndex;
        int totalItems = ParentPage.MediaFlipView.Items.Count;
        int searchIndex = currentIndex;
        bool foundVideo = false;

        // Reset the current video to the beginning before advancing
        if (VideoElement?.Visibility == Visibility.Visible &&
            VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter currentPresenter &&
            currentPresenter.MediaPlayer != null)
        {
            currentPresenter.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            // Save the reset state
            SavePlaybackState();
        }

        // Search for the next video, starting from the next item
        for (int i = 1; i < totalItems; i++)
        {
            searchIndex = (currentIndex + i) % totalItems;

            // Check if we've wrapped around back to the start in RepeatAll mode
            if (Settings.Current.RepeatMode == RepeatMode.RepeatAll && searchIndex == 0 && i > 1)
            {
                // We've looped back to the beginning, continue searching
            }
            else if (searchIndex <= currentIndex && i > 1)
            {
                // We've wrapped around and haven't found a video - stop searching
                break;
            }

            if (ParentPage.MediaFlipView.Items[searchIndex] is VisualPlayer candidatePlayer)
            {
                // Check if this item is a video
                if (candidatePlayer.CurrentFile != null)
                {
                    var mediaType = Files.GetMediaType(candidatePlayer.CurrentFile.Path);
                    if (mediaType == MediaType.Video)
                    {
                        foundVideo = true;
                        break;
                    }
                }
            }
        }

        // If no video was found, stop playback
        if (!foundVideo)
        {
            return;
        }

        // Move to the next video item
        ParentPage.MediaFlipView.SelectedIndex = searchIndex;

        // Get the next player and start playback
        if (ParentPage.MediaFlipView.Items[searchIndex] is VisualPlayer nextPlayer)
        {
            // Clear the saved playback state for the next video before loading
            if (nextPlayer.CurrentFile != null)
            {
                nextPlayer.ClearPlaybackState(nextPlayer.CurrentFile);
                Debug.WriteLine($"Cleared state for: {nextPlayer.CurrentFile.DisplayName}");
            }

            // Update the file info header and window title
            if (ParentPage?.ParentPage != null && nextPlayer.CurrentFile != null)
            {
                ParentPage.ParentPage.DataContext = nextPlayer.Properties;
                App.Current.ActiveWindow.Title = nextPlayer.CurrentFile.DisplayName + nextPlayer.CurrentFile.FileType;
            }

            // Start playing the next video if it's a video
            if (nextPlayer.VideoElement?.Visibility == Visibility.Visible &&
                nextPlayer.VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter &&
                presenter.MediaPlayer != null)
            {
                // Reset position to beginning before playing
                presenter.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;

                // Apply the current repeat mode to the next player
                presenter.MediaPlayer.IsLoopingEnabled = (Settings.Current.RepeatMode == RepeatMode.RepeatOne);

                presenter.MediaPlayer.Play();
            }
        }
    }
    
    public async Task TogglePlayPause() 
    {
        if (ImageElement.Visibility == Visibility.Visible && GifImage != null)
        {
            MediaButton mediaButton = (MediaButton)ImageElement.Tag;
            if (!isPaused)
            {
                GifImage.Stop();
                mediaButton.Pause();
                isPaused = true;
            }
            else if (isPaused)
            {
                GifImage.Play(); 
                mediaButton.Play();
                isPaused = false;
            }
        }
        else if (VideoElement.Visibility == Visibility.Visible)
        {
            if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter && presenter.MediaPlayer != null)
            {
                MediaButton mediaButton = (MediaButton)presenter.Tag;
                if (!isPaused)
                {
                    presenter.MediaPlayer.Pause();
                }
                else if (isPaused)
                {
                    presenter.MediaPlayer.Play();
                }
            }
        }
    }



    private void ImageElement_ImageOpened(object sender, RoutedEventArgs e)
    {
        LoadRing.IsActive = false;
    }

    private void ImageElement_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (GifImage != null)
        {
            Image image = (Image)sender;
            MediaButton mediaButton = (MediaButton)image.Tag;

            if (!isPaused)
            {
                GifImage.Stop();
                mediaButton.Pause();
                isPaused = true;
            }
            else if (isPaused)
            {
                GifImage.Play();
                mediaButton.Play();
                isPaused = false;
            }
        }
    }

    private async void ImageElement_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var s = (FrameworkElement)sender;
        var d = s.DataContext;

        MenuFlyout Flyout = new MenuFlyout();

        //Share Item
        MenuFlyoutItem ShareItem = new MenuFlyoutItem { Text = $"Share", Icon = new SymbolIcon(Symbol.Share) };
        ShareItem.Click += async (_, __) => await ShareCurrentFrame();
        Flyout.Items.Add(ShareItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Open With Item
        MenuFlyoutItem OpenWithItem = new MenuFlyoutItem { Text = $"Open With", Icon = new SymbolIcon(Symbol.OpenWith) };
        OpenWithItem.Click += async (_, __) => await Launcher.LaunchFileAsync(CurrentFile, new LauncherOptions() { DisplayApplicationPicker = true });
        Flyout.Items.Add(OpenWithItem);

        //Save As Item
        MenuFlyoutItem SaveAsItem = new MenuFlyoutItem { Text = $"Save Frame As", Icon = new SymbolIcon(Symbol.SaveLocal) };
        SaveAsItem.Click += async (_, __) => await SaveFrameToFile();
        Flyout.Items.Add(SaveAsItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Edit 
        MenuFlyoutSubItem EditItem = new MenuFlyoutSubItem { Text = $"Edit", Icon = new SymbolIcon(Symbol.Edit) };

        //Edit Video
        IconElement? editvideoicon = Files.GetAppDisplayName(Settings.Current.VideoEditorPath) != null
             ? await Files.GetAppIconAsync(Settings.Current.VideoEditorPath)
             : new SymbolIcon(Symbol.Edit);
        MenuFlyoutItem EditVideoItem = new MenuFlyoutItem { Text = $"Edit Video", Icon = editvideoicon };
        EditVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Editor);
        EditItem.Items.Add(EditVideoItem);

        //Edit Frame
        IconElement? editframeicon = Files.GetAppDisplayName(Settings.Current.ImageEditorPath) != null
             ? await Files.GetAppIconAsync(Settings.Current.ImageEditorPath)
             : new SymbolIcon(Symbol.Edit);
        MenuFlyoutItem EditFrameItem = new MenuFlyoutItem { Text = $"Edit Frame", Icon = editframeicon };
        EditFrameItem.Click += async (_, __) => await EditCurrentFrame();
        EditItem.Items.Add(EditFrameItem);

        //Separator
        EditItem.Items.Add(new MenuFlyoutSeparator());

        //Converter to Gif
        MenuFlyoutItem ConvertToGifItem = new MenuFlyoutItem { Text = $"Convert to Gif", Icon = new FontIcon() { Glyph = "\uF4A9" } };
        EditItem.Items.Add(ConvertToGifItem);

        EditItem.Items.Add(new MenuFlyoutSeparator());

        //Video Converter

        if (File.Exists(Settings.))
        {
            MenuFlyoutItem ConvertVideoItem = new MenuFlyoutItem { Text = $"Convert Video", Icon = await Files.GetAppIconAsync(Settings.Current.VideoConverterPath) };
            ConvertVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Converter); ;
            EditItem.Items.Add(ConvertVideoItem);
            EditItem.Items.Add(new MenuFlyoutSeparator());
        }

        //Video Upscaler
        if (File.Exists(Settings.Current.VideoUpscalerPath))
        {
            MenuFlyoutItem UpscaleVideoItem = new MenuFlyoutItem { Text = $"Upscale Video", Icon = await Files.GetAppIconAsync(Settings.Current.VideoUpscalerPath) };
            UpscaleVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Upscaler); ;
            EditItem.Items.Add(UpscaleVideoItem);
        }
         
        //Image Upscaler
        if (File.Exists(Settings.Current.ImageUpscalerPath))
        {
            MenuFlyoutItem UpscaleFrameItem = new MenuFlyoutItem { Text = $"Upscale Frame", Icon = await Files.GetAppIconAsync(Settings.Current.ImageUpscalerPath) };
            UpscaleFrameItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Upscaler); ;
            EditItem.Items.Add(UpscaleFrameItem);
        }

        Flyout.Items.Add(EditItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Copy Frame Item
        MenuFlyoutItem CopyFrameItem = new MenuFlyoutItem { Text = $"Copy Frame", Icon = new SymbolIcon(Symbol.Copy) };
        CopyFrameItem.Click += async (_, __) => CopyFrameToClipboard();
        Flyout.Items.Add(CopyFrameItem);

        //Copy as Path Item
        MenuFlyoutItem CopyAsPathItem = new MenuFlyoutItem { Text = $"Copy as Path", Icon = new FontIcon() { Glyph = "\uE62F" } };
        CopyAsPathItem.Click += async (_, __) => { CurrentFile.CopyFilePathToClipboard(); };
        Flyout.Items.Add(CopyAsPathItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Show in File Explorer Item
        MenuFlyoutItem OpenFileExplorerItem = new MenuFlyoutItem { Text = $"Show in File Explorer", Icon = new SymbolIcon(Symbol.OpenFile) };
        OpenFileExplorerItem.Click += async (_, __) => await CurrentFile.ShowInFileExplorerAsync();
        Flyout.Items.Add(OpenFileExplorerItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Fit to Window Item
        MenuFlyoutItem FitItem = new MenuFlyoutItem { Text = $"Fit to Window", Icon = new FontIcon() { Glyph = "\uE9A6" } };
        FitItem.Click += async (_, __) => FitContentToWidth();
        Flyout.Items.Add(FitItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Reverse Image Search Item
        MenuFlyoutItem ReverseImageSearchItem = new MenuFlyoutItem { Text = $"Reverse Image Search", Icon = new FontIcon() { Glyph = "\uF6FA" } };
        ReverseImageSearchItem.Click += async (_, __) => await Launcher.LaunchUriAsync(new Uri(Settings.Current.ReverseImageSearchProvider));
        Flyout.Items.Add(ReverseImageSearchItem);

        //File Information Item
        MenuFlyoutItem FileInformationItem = new MenuFlyoutItem { Text = $"File Information", Icon = new FontIcon() { Glyph = "\uE946" } };
        FileInformationItem.Click += async (_, __) => ParentPage?.ParentPage?.MainViewContent.IsPaneOpen = true;
        Flyout.Items.Add(FileInformationItem);

        Flyout.ShowAt(s, e.GetPosition(s));
    }

    private async void ImageElement_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        // Cancel drag if in free move mode
        if (isFreeMove)
        {
            args.Cancel = true;
            return;
        }

        // Only allow dragging if we have a valid current file
        if (CurrentFile == null)
        {
            args.Cancel = true;
            return;
        }

        try
        {
            // Get the deferral to perform async operations
            var deferral = args.GetDeferral();

            // Create a resized bitmap for the drag visual
            var bitmap = new BitmapImage();

            // Set DecodePixelWidth before SetSourceAsync
            bitmap.DecodePixelWidth = 200;

            using (var stream = await CurrentFile.OpenReadAsync())
            {
                await bitmap.SetSourceAsync(stream);
            }

            // Set the drag visual with the resized bitmap
            args.DragUI.SetContentFromBitmapImage(bitmap);

            // Add the storage file to the data package
            args.Data.SetStorageItems(new List<IStorageItem> { CurrentFile });

            // Set additional data formats for compatibility
            args.Data.RequestedOperation = DataPackageOperation.Copy;

            // Optionally add a text representation (file path)
            args.Data.Properties.Title = CurrentFile.DisplayName;
            args.Data.Properties.Description = $"{CurrentFile.DisplayType} file";

            // Add bitmap data for direct image pasting (full size, not the preview)
            var fileStream = await CurrentFile.OpenReadAsync();
            args.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(fileStream));

            deferral.Complete();
        }
        catch (Exception ex)
        {
            // Log or handle the error
            Debug.WriteLine($"Error during image drag: {ex.Message}");
            args.Cancel = true;
        }
    }

    private void VisualPlayerPresenter_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Grid GD = (Grid)sender;
        MediaPlayerPresenter presenter = (MediaPlayerPresenter)GD.Tag;
        MediaButton mediaButton = (MediaButton)presenter.Tag;

        if (!isPaused)
        {
            presenter.MediaPlayer.Pause();
        }
        else if (isPaused)
        {
            presenter.MediaPlayer.Play();
        }
    }

    private async void VisualPlayerPresenter_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var s = (FrameworkElement)sender;
        var d = s.DataContext;

        MenuFlyout Flyout = new MenuFlyout();

        //Share Item
        MenuFlyoutItem ShareItem = new MenuFlyoutItem { Text = $"Share", Icon = new SymbolIcon(Symbol.Share) };
        ShareItem.Click += async (_, __) => await ShareCurrentFrame();
        Flyout.Items.Add(ShareItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Open With Item
        MenuFlyoutItem OpenWithItem = new MenuFlyoutItem { Text = $"Open With", Icon = new SymbolIcon(Symbol.OpenWith) };
        OpenWithItem.Click += async (_, __) => await Launcher.LaunchFileAsync(CurrentFile, new LauncherOptions() { DisplayApplicationPicker = true });
        Flyout.Items.Add(OpenWithItem);

        //Save As Item
        MenuFlyoutItem SaveAsItem = new MenuFlyoutItem { Text = $"Save Frame As", Icon = new SymbolIcon(Symbol.SaveLocal) };
        SaveAsItem.Click += async (_, __) => await SaveFrameToFile();
        Flyout.Items.Add(SaveAsItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Edit 
        MenuFlyoutSubItem EditItem = new MenuFlyoutSubItem { Text = $"Edit", Icon = new SymbolIcon(Symbol.Edit) };

        //Edit Video
        IconElement? editvideoicon = Files.GetAppDisplayName(Settings.Current.VideoEditorPath) != null
             ? await Files.GetAppIconAsync(Settings.Current.VideoEditorPath)
             : new SymbolIcon(Symbol.Edit);
        MenuFlyoutItem EditVideoItem = new MenuFlyoutItem { Text = $"Edit Video", Icon = editvideoicon };
        EditVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Editor);
        EditItem.Items.Add(EditVideoItem);

        //Edit Frame
        IconElement? editframeicon = Files.GetAppDisplayName(Settings.Current.ImageEditorPath) != null
             ? await Files.GetAppIconAsync(Settings.Current.ImageEditorPath)
             : new SymbolIcon(Symbol.Edit);
        MenuFlyoutItem EditFrameItem = new MenuFlyoutItem { Text = $"Edit Frame", Icon = editframeicon };
        EditFrameItem.Click += async (_, __) => await EditCurrentFrame();
        EditItem.Items.Add(EditFrameItem);

        //Separator
        EditItem.Items.Add(new MenuFlyoutSeparator());

        //Converter to Gif
        MenuFlyoutItem ConvertToGifItem = new MenuFlyoutItem { Text = $"Convert to Gif", Icon = new FontIcon() { Glyph = "\uF4A9" } };
        EditItem.Items.Add(ConvertToGifItem);

        EditItem.Items.Add(new MenuFlyoutSeparator());

        //Video Converter
        if (!string.IsNullOrEmpty(Settings.Current.VideoConverterPath))
        {
            MenuFlyoutItem ConvertVideoItem = new MenuFlyoutItem { Text = $"Convert Video", Icon = await Files.GetAppIconAsync(Settings.Current.VideoConverterPath) };
            ConvertVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Converter); ;
            EditItem.Items.Add(ConvertVideoItem);
            EditItem.Items.Add(new MenuFlyoutSeparator());
        }

        //Video Upscaler
        if (!string.IsNullOrEmpty(Settings.Current.VideoUpscalerPath))
        {
            MenuFlyoutItem UpscaleVideoItem = new MenuFlyoutItem { Text = $"Upscale Video", Icon = await Files.GetAppIconAsync(Settings.Current.VideoUpscalerPath) };
            UpscaleVideoItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Upscaler); ;
            EditItem.Items.Add(UpscaleVideoItem);
        }
         
        //Image Upscaler
        if (!string.IsNullOrEmpty(Settings.Current.ImageUpscalerPath))
        {
            MenuFlyoutItem UpscaleFrameItem = new MenuFlyoutItem { Text = $"Upscale Frame", Icon = await Files.GetAppIconAsync(Settings.Current.ImageUpscalerPath) };
            UpscaleFrameItem.Click += async (_, __) => MediaExtensions.OpenFile(CurrentFile, OpenAction.Upscaler); ;
            EditItem.Items.Add(UpscaleFrameItem);
        }

        Flyout.Items.Add(EditItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Copy Frame Item
        MenuFlyoutItem CopyFrameItem = new MenuFlyoutItem { Text = $"Copy Frame", Icon = new SymbolIcon(Symbol.Copy) };
        CopyFrameItem.Click += async (_, __) => CopyFrameToClipboard();
        Flyout.Items.Add(CopyFrameItem);

        //Copy as Path Item
        MenuFlyoutItem CopyAsPathItem = new MenuFlyoutItem { Text = $"Copy as Path", Icon = new FontIcon() { Glyph = "\uE62F" } };
        CopyAsPathItem.Click += async (_, __) => { CurrentFile.CopyFilePathToClipboard(); };
        Flyout.Items.Add(CopyAsPathItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Show in File Explorer Item
        MenuFlyoutItem OpenFileExplorerItem = new MenuFlyoutItem { Text = $"Show in File Explorer", Icon = new SymbolIcon(Symbol.OpenFile) };
        OpenFileExplorerItem.Click += async (_, __) => await CurrentFile.ShowInFileExplorerAsync();
        Flyout.Items.Add(OpenFileExplorerItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Fit to Window Item
        MenuFlyoutItem FitItem = new MenuFlyoutItem { Text = $"Fit to Window", Icon = new FontIcon() { Glyph = "\uE9A6" } };
        FitItem.Click += async (_, __) => FitContentToWidth();
        Flyout.Items.Add(FitItem);

        //Separator
        Flyout.Items.Add(new MenuFlyoutSeparator());

        //Reverse Image Search Item
        MenuFlyoutItem ReverseImageSearchItem = new MenuFlyoutItem { Text = $"Reverse Image Search", Icon = new FontIcon() { Glyph = "\uF6FA" } };
        ReverseImageSearchItem.Click += async (_, __) => await Launcher.LaunchUriAsync(new Uri(Settings.Current.ReverseImageSearchProvider));
        Flyout.Items.Add(ReverseImageSearchItem);

        //File Information Item
        MenuFlyoutItem FileInformationItem = new MenuFlyoutItem { Text = $"File Information", Icon = new FontIcon() { Glyph = "\uE946" } };
        FileInformationItem.Click += async (_, __) => ParentPage?.ParentPage?.MainViewContent.IsPaneOpen = true;
        Flyout.Items.Add(FileInformationItem);

        Flyout.ShowAt(s, e.GetPosition(s));
    }



    private void ImageScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        ImageScrollViewer = (ScrollViewer)sender;

        ImageScrollViewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ScrollViewer_PointerPressed), true);
        ImageScrollViewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ScrollViewer_PointerReleased), true);

        ImageScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ImageScrollViewer_PointerWheelChanged), true);
    }

    private void VideoScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        VideoScrollViewer = (ScrollViewer)sender;
        VideoScrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ScrollViewer_PointerWheelChanged), true);

        // Add handlers for tracking right button state
        VideoScrollViewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(VideoScrollViewer_PointerPressed), true);
        VideoScrollViewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(VideoScrollViewer_PointerReleased), true);
    }


    private bool _isDragging = false;
    private Point _lastPoint;

    private void ScrollViewer_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if ((ScrollViewer)sender == ImageScrollViewer)
        {
            //SimulateCtrlKeyPress(true);
        }
    }

    private void ScrollViewer_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isDragging = false;
        //SimulateCtrlKeyPress(false);
    }

    private async void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ScrollViewer ScrollViewer = (ScrollViewer)sender;
        //Viewbox viewbox = (Viewbox)ScrollViewer.Content;

        //MediaCanvas canvas = (MediaCanvas)ScrollViewer.Tag;
        //Grid root = (Grid)canvas.Parent

        if (isFreeMove == false)
        {
            _isDragging = true;
            _lastPoint = e.GetCurrentPoint(ScrollViewer).Position;
            ScrollViewer.CapturePointer(e.Pointer);

            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }

        var pointProps = e.GetCurrentPoint(ScrollViewer).Properties;
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && pointProps.IsMiddleButtonPressed)
        {
            isFreeMove = !isFreeMove;
            await ToggleFreeMove(isFreeMove);
        }
    }


    private void ScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ScrollViewer ScrollViewer = (ScrollViewer)sender;

        if (isFreeMove == false)
        {         
            _isDragging = false;
            ScrollViewer.ReleasePointerCaptures();

            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }
    }

    private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ScrollViewer ScrollViewer = (ScrollViewer)sender;      

        if (!_isDragging)
            return;

        Point currentPoint = e.GetCurrentPoint(ScrollViewer).Position;
        double deltaX = currentPoint.X - _lastPoint.X;
        double deltaY = currentPoint.Y - _lastPoint.Y;

        // Reverse the direction for natural dragging
        ScrollViewer.ChangeView(
            ScrollViewer.HorizontalOffset - deltaX,
            ScrollViewer.VerticalOffset - deltaY,
            null,
            disableAnimation: true);

        _lastPoint = currentPoint;
    }

    private void ImageScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;

        // Only intercept mouse wheel events - let touch/pen pass through normally
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            e.Handled = false; // Let ScrollViewer handle touch/pen
            return;
        }

        var sv = (ScrollViewer)sender;
        var props = e.GetCurrentPoint(sv).Properties;
        int delta = props.MouseWheelDelta; // typically +/-120 per notch

        if (delta == 0)
            return;

        // Calculate zoom step - Photos app uses approximately 10% multiplicative zoom per notch
        float currentZoom = sv.ZoomFactor;
        float zoomMultiplier = 1.25f; // 10% change per notch (matches Photos app)
        float newZoom;

        if (delta > 0)
        {
            // Zoom in: multiply by 1.1 per notch
            newZoom = currentZoom * (float)Math.Pow(zoomMultiplier, Math.Abs(delta) / 120.0);
            newZoom = Math.Min(sv.MaxZoomFactor, newZoom);
        }
        else
        {
            // Zoom out: divide by 1.1 per notch
            newZoom = currentZoom / (float)Math.Pow(zoomMultiplier, Math.Abs(delta) / 120.0);
            newZoom = Math.Max(sv.MinZoomFactor, newZoom);
        }

        // Get the pointer position relative to the ScrollViewer to zoom towards cursor
        var pointerPos = e.GetCurrentPoint(sv).Position;

        // Calculate the center point for zoom
        double centerX = (sv.HorizontalOffset + pointerPos.X) / currentZoom;
        double centerY = (sv.VerticalOffset + pointerPos.Y) / currentZoom;

        // Apply new zoom
        sv.ChangeView(
            centerX * newZoom - pointerPos.X,
            centerY * newZoom - pointerPos.Y,
            newZoom,
            disableAnimation: false);

         // Prevent ScrollViewer from processing this mouse wheel event
    }


    private bool _isRightButtonPressed = false;

    private void VideoScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        var props = e.GetCurrentPoint(sv).Properties;

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && props.IsRightButtonPressed)
        {
            _isRightButtonPressed = true;
        }
    }

    private void VideoScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        var props = e.GetCurrentPoint(sv).Properties;

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !props.IsRightButtonPressed)
        {
            _isRightButtonPressed = false;
        }
    }




    /// <summary>
    /// Handle mouse wheel on the ScrollViewer used for video. When a video is visible the wheel will control the VisualPlayer volume.
    /// </summary>
    private void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Only act for mouse wheel events.
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            return;

        // Only process when a video is currently visible.
        if (VideoElement.Visibility != Visibility.Visible)
            return;

        var sv = (ScrollViewer)sender;
        var props = e.GetCurrentPoint(sv).Properties;
        int delta = props.MouseWheelDelta; // typically +/-120 per notch
        if (delta == 0)
            return;

        // Check if right button is held - if so, zoom instead of adjusting volume
        if (_isRightButtonPressed)
        {
            e.Handled = true;

            // Calculate zoom step - similar to image zoom behavior
            float currentZoom = sv.ZoomFactor;
            float zoomMultiplier = 1.25f; // 25% change per notch
            float newZoom;

            if (delta > 0)
            {
                // Zoom in: multiply by 1.25 per notch
                newZoom = currentZoom * (float)Math.Pow(zoomMultiplier, Math.Abs(delta) / 120.0);
                newZoom = Math.Min(sv.MaxZoomFactor, newZoom);
            }
            else
            {
                // Zoom out: divide by 1.25 per notch
                newZoom = currentZoom / (float)Math.Pow(zoomMultiplier, Math.Abs(delta) / 120.0);
                newZoom = Math.Max(sv.MinZoomFactor, newZoom);
            }

            // Get the pointer position relative to the ScrollViewer to zoom towards cursor
            var pointerPos = e.GetCurrentPoint(sv).Position;

            // Calculate the center point for zoom
            double centerX = (sv.HorizontalOffset + pointerPos.X) / currentZoom;
            double centerY = (sv.VerticalOffset + pointerPos.Y) / currentZoom;

            // Apply new zoom
            sv.ChangeView(
                centerX * newZoom - pointerPos.X,
                centerY * newZoom - pointerPos.Y,
                newZoom,
                disableAnimation: false);

            return;
        }

        // Default behavior: adjust volume
        // Find the active MediaPlayer.
        if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter && presenter.MediaPlayer != null)
        {
            var player = presenter.MediaPlayer;

            // Use 5% volume step per wheel notch (scaled if the delta is larger).
            double step = 0.05 * (Math.Abs(delta) / 120.0);

            if (delta > 0)
                player.Volume = Math.Min(1.0, player.Volume + step);
            else
                player.Volume = Math.Max(0.0, player.Volume - step);

            // Prevent the ScrollViewer from scrolling when adjusting volume.
            e.Handled = true;
        }
    }

    private void ScrollViewer_LostFocus(object sender, RoutedEventArgs e)
    {
        _isDragging = false;
        SimulateCtrlKeyPress(false);
    }

    private void SimulateCtrlKeyPress(bool pressDown)
    {
        var injector = InputInjector.TryCreate();

        if (injector != null)
        {
            var info = new InjectedInputKeyboardInfo();
            info.VirtualKey = (ushort)VirtualKey.Control;
            info.KeyOptions = pressDown ?
                InjectedInputKeyOptions.None :
                InjectedInputKeyOptions.KeyUp;

            injector.InjectKeyboardInput(new[] { info });
        }
    }




    /// <summary>
    /// Adjust the ScrollViewer zoom and offsets so the entire image width is visible.
    /// If the ScrollViewer has been moved into a VisualCanvas (free-move mode) this also
    /// ensures the left/top canvas offsets are reset so the image isn't clipped.
    /// </summary>
    public void FitContentToWidth()
    {
        try
        {
            ScrollViewer activeScrollViewer = null;
            FrameworkElement contentElement = null;

            if (ImageElement.Visibility == Visibility.Visible && ImageScrollViewer != null)
            {
                activeScrollViewer = ImageScrollViewer;
                if (ImageScrollViewer.Content is Grid grid && grid.Children.Count > 0 && grid.Children[0] is FrameworkElement inner)
                {
                    contentElement = inner;
                }
            }
            else if (VideoElement.Visibility == Visibility.Visible && VideoScrollViewer != null)
            {
                activeScrollViewer = VideoScrollViewer;
                if (VideoScrollViewer.Content is Viewbox viewbox && viewbox.Child is FrameworkElement videoInner)
                {
                    contentElement = videoInner;
                }
            }

            if (activeScrollViewer == null || contentElement == null) return;

            double contentWidth = contentElement.ActualWidth;
            double viewportWidth = activeScrollViewer.ViewportWidth;

            // If layout not ready yet, try again shortly.
            if (contentWidth <= 0 || viewportWidth <= 0)
            {
                _ = this.DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(20);
                    FitContentToWidth();
                });
                return;
            }

            // Ensure content is stretched
            contentElement.HorizontalAlignment = HorizontalAlignment.Stretch;
            contentElement.VerticalAlignment = VerticalAlignment.Stretch;

            // Compute a zoom so the full width of the content fits into the viewport.
            float targetZoom = (float)(viewportWidth / contentWidth);

            // Clamp zoom to 1.0 (do not upscale beyond 100%)
            targetZoom = Math.Min(1.0f, targetZoom);

            // Reset offsets and apply the zoom.
            activeScrollViewer.ChangeView(0, 0, targetZoom, disableAnimation: true);

            // Center content if smaller than viewport
            if (targetZoom == 1.0f && contentWidth < viewportWidth)
            {
                double offset = (viewportWidth - contentWidth) / 2;
                activeScrollViewer.ChangeView(offset, 0, targetZoom, disableAnimation: true);
            }

            // If the ScrollViewer is in free-move mode inside a VisualCanvas, ensure canvas offsets don't hide the left edge.
            if (activeScrollViewer.Parent is VisualCanvas)
            {
                Canvas.SetLeft(activeScrollViewer, 0);
                Canvas.SetTop(activeScrollViewer, 0);
            }
        }
        catch
        {
            // Intentionally ignore layout/measure timing exceptions.
        }
    }

    #region Free Move

    ScrollViewer ImageScrollViewer;
    ScrollViewer VideoScrollViewer;

    bool isFreeMoveDragging = false;
    Pointer draggingPointer;
    Point dragStartPosition;
    double originalLeft, originalTop;

    public async Task ToggleFreeMove(bool Enable)
    {
        ScrollViewer ScrollViewer = new();
        Grid ImageGrid = new();
        Viewbox VideoBox = new();

        if (ImageElement.Visibility == Visibility.Visible)
        {
            ScrollViewer = ImageScrollViewer;
            ImageGrid = (Grid)ScrollViewer.Content;
        }
        else if (VideoElement.Visibility == Visibility.Visible)
        {
            ScrollViewer = VideoScrollViewer;
            VideoBox = (Viewbox)ScrollViewer.Content;
        }

        if (ScrollViewer == null) return;

        VisualCanvas canvas = (VisualCanvas)ScrollViewer.Tag;
        Grid root = (Grid)canvas.Parent;

        if (Enable == true)
        {
            if (root.Children.Contains(ScrollViewer))
            {
                float zoom = ScrollViewer.ZoomFactor;

                FrameworkElement innerContent = ImageGrid;
                if (ImageElement.Visibility == Visibility.Visible)
                {
                    innerContent = (FrameworkElement)ImageGrid.Children[0];
                }
                else if (VideoElement.Visibility == Visibility.Visible)
                {
                    innerContent = (FrameworkElement)VideoBox.Child;
                }

                double originalWidth = innerContent.ActualWidth;
                double originalHeight = innerContent.ActualHeight;
                double zoomedWidth = originalWidth * zoom;
                double zoomedHeight = originalHeight * zoom;

                ScrollViewer.Width = zoomedWidth;
                ScrollViewer.Height = zoomedHeight;

                root.Children.Remove(ScrollViewer);
                canvas.Children.Add(ScrollViewer);
            }
        }
        else
        {
            if (canvas.Children.Contains(ScrollViewer))
            {
                ScrollViewer.ClearValue(WidthProperty);
                ScrollViewer.ClearValue(HeightProperty);

                canvas.Children.Remove(ScrollViewer);
                root.Children.Insert(1, ScrollViewer);
            }

        }

        isFreeMove = Enable;
    }

    private async void FreeMoveCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        VisualCanvas canvas = (VisualCanvas)sender;
        ScrollViewer view = (ScrollViewer)canvas.Tag;
        Grid root = (Grid)canvas.Parent;

        var pointProps = e.GetCurrentPoint(view).Properties;

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            canvas.InputCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);

            isFreeMoveDragging = true;
            draggingPointer = e.Pointer;

            dragStartPosition = e.GetCurrentPoint(canvas).Position;
            originalLeft = Canvas.GetLeft(view);
            originalTop = Canvas.GetTop(view);

            view.CapturePointer(draggingPointer);
        }
    }

    private void FreeMoveCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        VisualCanvas canvas = (VisualCanvas)sender;
        ScrollViewer view = (ScrollViewer)canvas.Tag;

        if (isFreeMoveDragging && e.Pointer.PointerId == draggingPointer.PointerId)
        {
            Point currentPosition = e.GetCurrentPoint(canvas).Position;

            double offsetX = currentPosition.X - dragStartPosition.X;
            double offsetY = currentPosition.Y - dragStartPosition.Y;

            Canvas.SetLeft(view, originalLeft + offsetX);
            Canvas.SetTop(view, originalTop + offsetY);
        }
    }

    private void FreeMoveCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        VisualCanvas canvas = (VisualCanvas)sender;
        ScrollViewer view = (ScrollViewer)canvas.Tag;

        if (isFreeMoveDragging && e.Pointer.PointerId == draggingPointer.PointerId)
        {
            canvas.InputCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

            isFreeMoveDragging = false;
            view.ReleasePointerCaptures();
        }
    }

    #endregion


    #region Frame Copying

    /// <summary>
    /// Extracts the current frame as a file. For images, returns the current file.
    /// For videos, extracts the current frame to a temporary file.
    /// </summary>
    private async Task<FrameExtractionResult> ExtractCurrentFrameAsync()
    {
        // Handle image case - just return the current file
        if (ImageElement.Visibility == Visibility.Visible && CurrentFile != null)
        {
            var fileStream = await CurrentFile.OpenReadAsync();
            var memoryStream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(fileStream, memoryStream);
            memoryStream.Seek(0);
            fileStream.Dispose();

            return new FrameExtractionResult
            {
                File = CurrentFile,
                Stream = memoryStream,
                FileName = CurrentFile.DisplayName,
                Description = $"{CurrentFile.DisplayType} file",
                IsTemporary = false
            };
        }

        // Handle video case - extract current frame
        if (VideoElement.Visibility == Visibility.Visible)
        {
            if (!(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
                presenter.MediaPlayer == null)
            {
                return null;
            }

            // Get current playback position
            TimeSpan position = presenter.MediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;

            // Build frame file path
            string timestamp = position.FormatTimestampForFilename();
            string baseName = Path.GetFileNameWithoutExtension(CurrentFile.Path);
            string tempFileName = $"{baseName}_frame_{timestamp}.png";
            string tempPath = Path.Combine((await Folders.GetTempFolder()).Path, tempFileName);

            // Extract the frame using FFmpeg
            await MediaExtensions.ExtractVideoFrameAsync(CurrentFile.Path, position, tempPath, isHDR);

            // Load the extracted frame
            var frameFile = await StorageFile.GetFileFromPathAsync(tempPath);
            var fileStream = await frameFile.OpenReadAsync();
            var memoryStream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(fileStream, memoryStream);
            memoryStream.Seek(0);
            fileStream.Dispose();

            return new FrameExtractionResult
            {
                File = frameFile,
                Stream = memoryStream,
                FileName = tempFileName,
                Description = $"Video frame at {position:hh\\:mm\\:ss\\.fff}",
                IsTemporary = true
            };
        }

        return null;
    }



    /// <summary>
    /// Saves a frame to a user-selected file.
    /// </summary>
    /// <returns></returns>
    public async Task SaveFrameToFile()
    {

        // Handle image saving when ImageElement is visible
        if (ImageElement.Visibility == Visibility.Visible && ImageElement.Source != null)
        {
            string baseName = Path.GetFileNameWithoutExtension(CurrentFile.Path);

            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(App.Current.ActiveWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.SuggestedFileName = baseName;

            picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });
            picker.FileTypeChoices.Add("JPEG", new List<string> { ".jpg", ".jpeg" });
            picker.FileTypeChoices.Add("BMP", new List<string> { ".bmp" });
            picker.FileTypeChoices.Add("TIFF", new List<string> { ".tiff", ".tif" });
            picker.FileTypeChoices.Add("WEBP", new List<string> { ".webp" });
            picker.FileTypeChoices.Add("GIF", new List<string> { ".gif" });

            var dest = await picker.PickSaveFileAsync();
            if (dest == null) return; // user cancelled

            // If the current file is a GIF and the user selects a static image format
            var ext = Path.GetExtension(dest.Path).ToLowerInvariant();
            var isGif = Files.GetMediaType(CurrentFile.Path) == MediaType.Gif;
            bool isStaticImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".tiff" || ext == ".tif" || ext == ".webp";

            if (isGif && isStaticImage)
            {
                Guid encoderId = ext switch
                {
                    ".png" => Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    ".jpg" or ".jpeg" => Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId,
                    ".bmp" => Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId,
                    ".tiff" or ".tif" => Windows.Graphics.Imaging.BitmapEncoder.TiffEncoderId,
                    ".webp" => Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    _ => Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId
                };

                await CurrentFile.ConvertGifToImageAsync(dest, encoderId);
            }
            else
            {
                // Copy the file to the destination
                await CurrentFile.CopyAndReplaceAsync(dest);
            }
            return;
        }

        // Handle video case
        if (VideoElement.Visibility == Visibility.Visible)
        {
            if (!(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
                presenter.MediaPlayer == null)
            {
                return;
            }

            TimeSpan position = presenter.MediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;
            string timestamp = position.FormatTimestampForFilename();
            string baseName = Path.GetFileNameWithoutExtension(CurrentFile.Path);
            string suggestedName = $"{baseName}_{timestamp}";

            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(App.Current.ActiveWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.SuggestedFileName = suggestedName;

            picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });
            picker.FileTypeChoices.Add("JPEG", new List<string> { ".jpg", ".jpeg" });
            picker.FileTypeChoices.Add("BMP", new List<string> { ".bmp" });
            picker.FileTypeChoices.Add("TIFF", new List<string> { ".tiff", ".tif" });
            picker.FileTypeChoices.Add("WEBP", new List<string> { ".webp" });
            picker.FileTypeChoices.Add("GIF (single frame)", new List<string> { ".gif" });

            var dest = await picker.PickSaveFileAsync();
            if (dest == null) return; // user cancelled

            await MediaExtensions.ExtractVideoFrameAsync(CurrentFile.Path, position, dest.Path, isHDR);
        }
    }

    /// <summary>
    /// Copies the frame to the clipboard.
    /// </summary>
    /// <returns></returns>
    public async Task CopyFrameToClipboard()
    {
        try
        {
            var frameResult = await ExtractCurrentFrameAsync();
            if (frameResult == null)
            {
                await MessageBox.Show("No active media found.", Title: "Copy Frame");
                return;
            }

            ShowClipboardFeedback();

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(frameResult.Stream));

            var dq = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;
            dq.TryEnqueue(() =>
            {
                Clipboard.SetContent(dataPackage);
            });

            // Clean up temporary files
            if (frameResult.IsTemporary)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Brief delay to ensure clipboard has the data
                    try
                    {
                        await frameResult.File.DeleteAsync();
                    }
                    catch { /* Ignore cleanup errors */ }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error copying frame to clipboard: {ex.Message}");
        }
    }


    public async Task EditCurrentFrame()
    {
        try
        {
            var frameResult = await ExtractCurrentFrameAsync();
            if (frameResult == null)
            {
                await MessageBox.Show("No content available to edit.", Title: "Edit");
                return;
            }

            // Launch the Windows Photos app with editing capabilities
            var options = new LauncherOptions
            {
                DisplayApplicationPicker = false,
                TargetApplicationPackageFamilyName = "Microsoft.Windows.Photos_8wekyb3d8bbwe"
            };

            bool launched = await Launcher.LaunchFileAsync(frameResult.File, options);

            if (!launched)
            {
                // Fallback: try to launch with default app and let user choose
                await Launcher.LaunchFileAsync(frameResult.File);
            }

            // Note: For temporary video frames, we don't delete them immediately
            // since the Photos app might still be accessing them. Consider cleanup
            // on app shutdown or periodic cleanup of temp folder.
        }
        catch (Exception ex)
        {
            await ErrorBox.Show(ex, Title: "Edit Failed");
        }
    }

    /// <summary>
    /// Shows feedback to the user similar to the snipping tool.
    /// </summary>
    private async void ShowClipboardFeedback()
    {
        // Create storyboard for the animation
        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

        // Opacity animation (fade in and out)
        var opacityAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacityAnimation, ClipboardFeedbackOverlay);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

        opacityAnimation.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(0),
            Value = 0
        });
        opacityAnimation.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(150),
            Value = 0.75,
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        });
        opacityAnimation.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(400),
            Value = 0.75
        });
        opacityAnimation.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(600),
            Value = 0,
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
        });

        storyboard.Children.Add(opacityAnimation);

        // Start animation
        storyboard.Begin();

        await Task.Delay(600);
    }


    #endregion


    #region Share Helpers


    private ShareData _pendingShareData;

    public async Task ShareCurrentFrame()
    {
        try
        {
            // Get the window handle for the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.ActiveWindow);

            // Use GetForWindow instead of GetForCurrentView for WinUI 3 desktop apps
            var dataTransferManager = Extensions.DataTransferManagerInterop.GetForWindow(hwnd);

            // Store the frame data in a field so it can be accessed in the DataRequested handler
            _pendingShareData = await PrepareShareData();

            if (_pendingShareData == null)
            {
                await MessageBox.Show("No content available to share.", Title: "Share");
                return;
            }

            // Register the DataRequested event handler
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;

            // Show the Windows share UI using the window handle
            Extensions.DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
        }
        catch (Exception ex)
        {
            await ErrorBox.Show(ex, Title: "Share Failed");
        }
    }

    private async Task<ShareData> PrepareShareData()
    {
        // Handle image sharing when ImageElement is visible
        if (ImageElement.Visibility == Visibility.Visible && CurrentFile != null)
        {
            var fileStream = await CurrentFile.OpenReadAsync();
            var memoryStream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(fileStream, memoryStream);
            memoryStream.Seek(0);
            fileStream.Dispose();

            return new ShareData
            {
                Title = CurrentFile.DisplayName,
                Description = $"{CurrentFile.DisplayType} file",
                Stream = memoryStream,
                StorageFile = CurrentFile
            };
        }
        // Handle video frame sharing
        else if (VideoElement.Visibility == Visibility.Visible)
        {
            if (!(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
                presenter.MediaPlayer == null)
            {
                return null;
            }

            // Get current playback position
            TimeSpan position = presenter.MediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;

            // Build a sanitized timestamp string
            int hours = (int)position.TotalHours;
            string timestamp = $"{hours:00}-{position.Minutes:00}-{position.Seconds:00}-{position.Milliseconds:000}";
            string baseName = Path.GetFileNameWithoutExtension(CurrentFile.Path);
            string tempName = $"{baseName}_frame_{timestamp}.png";
            string tempPath = Path.Combine((await Folders.GetTempFolder()).Path, tempName);

            string input = CurrentFile.Path;

            string filterChain = isHDR
                ? "zscale=t=linear:npl=100,zscale=t=bt709:m=bt709:r=tv,scale=in_range=tv:out_range=pc,format=rgb24"
                : "scale=in_range=tv:out_range=pc,format=rgb24";

            // Extract the frame
            await FFMpegArguments
                .FromFileInput(input, true, options => options.Seek(position))
                .OutputToFile(tempPath, overwrite: true, options => options
                    .WithCustomArgument($"-vf \"{filterChain}\"")
                    .WithCustomArgument("-frames:v 1")
                    .WithCustomArgument("-q:v 2"))
                .ProcessAsynchronously();

            // Load the temporary file
            var storageFile = await StorageFile.GetFileFromPathAsync(tempPath);
            var fileStream = await storageFile.OpenReadAsync();
            var memoryStream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(fileStream, memoryStream);
            memoryStream.Seek(0);
            fileStream.Dispose();

            return new ShareData
            {
                Title = tempName,
                Description = $"Video frame at {position:hh\\:mm\\:ss\\.fff}",
                Stream = memoryStream,
                StorageFile = storageFile,
                IsTemporary = true
            };
        }

        return null;
    }

    private async void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        // Unregister the event handler to prevent memory leaks
        sender.DataRequested -= DataTransferManager_DataRequested;
         
        if (_pendingShareData == null)
        {
            args.Request.FailWithDisplayText("No content available to share.");
            return;
        }

        var deferral = args.Request.GetDeferral();

        try
        {
            args.Request.Data.Properties.Title = _pendingShareData.Title;
            args.Request.Data.Properties.Description = _pendingShareData.Description;

            // Add the bitmap to the share data
            args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(_pendingShareData.Stream));

            // Also include the file if available
            if (_pendingShareData.StorageFile != null)
            {
                args.Request.Data.SetStorageItems(new List<IStorageItem> { _pendingShareData.StorageFile });
            }
        }
        catch (Exception ex)
        {
            args.Request.FailWithDisplayText($"Failed to prepare share data: {ex.Message}");
        }
        finally
        {
            deferral.Complete();

            // Clean up temporary files after a delay
            if (_pendingShareData?.IsTemporary == true && _pendingShareData?.StorageFile != null)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // Wait 5 seconds for share to complete
                    try
                    {
                        await _pendingShareData.StorageFile.DeleteAsync();
                    }
                    catch { /* Ignore cleanup errors */ }
                });
            }

            _pendingShareData = null;
        }
    }


    #endregion


    #region Video Scrubbing

    public double ScrubAmount { get; set; } = 0.033; // Default 1 frame   
    public readonly double[] ScrubAmounts = { 0.033, 1.0, 5.0, 10.0, 30.0, 60.0 }; // Frame, 1s, 5s, 10s, 30s, 1min
    public int ScrubAmountIndex { get; set; } = 0; // Start at 1 frame (index 0)
    public double VideoFrameRate { get; private set; } = 30.0;


    public void IncreaseScrubAmount()
    {
        if (ScrubAmountIndex < ScrubAmounts.Length - 1)
        {
            ScrubAmountIndex++;
            ScrubAmount = ScrubAmounts[ScrubAmountIndex];
        }
    }
      
    public void DecreaseScrubAmount()
    {
        if (ScrubAmountIndex > 0)
        {
            ScrubAmountIndex--;
            ScrubAmount = ScrubAmounts[ScrubAmountIndex];
        }
    }

    public string FormatScrubAmount()
    {
        if (ScrubAmount == 0.033)
        {
            return "1 frame";
        }
        else if (ScrubAmount < 1.0)
        {
            return $"{ScrubAmount:F1}s";
        }
        else if (ScrubAmount < 60.0)
        {
            return $"{ScrubAmount:F0}s";
        }
        else
        {
            int minutes = (int)(ScrubAmount / 60);
            return $"{minutes}m";
        }
    }

    public void PerformScrub(int direction)
    {
        if (VideoElement.Visibility != Visibility.Visible ||
            !(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
            presenter.MediaPlayer == null)
        {
            return;
        }

        var session = presenter.MediaPlayer.PlaybackSession;
        if (session == null) return;

        TimeSpan scrubAmount;

        // For frame-by-frame (0.033s), use actual framerate
        if (ScrubAmount == 0.033)
        {
            scrubAmount = TimeSpan.FromSeconds(1.0 / VideoFrameRate);
        }
        else
        {
            scrubAmount = TimeSpan.FromSeconds(ScrubAmount);
        }

        // Apply direction
        if (direction < 0)
        {
            scrubAmount = -scrubAmount;
        }

        var newPosition = session.Position + scrubAmount;

        // Clamp to valid range
        if (newPosition < TimeSpan.Zero)
            newPosition = TimeSpan.Zero;
        else if (newPosition > session.NaturalDuration)
            newPosition = session.NaturalDuration;

        session.Position = newPosition;
    }

    #endregion


    #region Playback Persistence

    private const string PLAYBACK_STATE_KEY = "VideoPlaybackState";
    private Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

    private void PlaybackSession_PositionChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
    {
        // Marshal to UI thread before accessing UI elements
        var dispatcherQueue = this.DispatcherQueue;
        if (dispatcherQueue != null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                // Handle marked section looping
                if (_isLoopingMarkedSection && _markInPosition.HasValue && _markOutPosition.HasValue)
                {
                    if (sender.Position >= _markOutPosition.Value)
                    {
                        // Loop back to Mark In position
                        sender.Position = _markInPosition.Value;
                    }
                    else if (sender.Position < _markInPosition.Value)
                    {
                        // If somehow position is before Mark In, jump to Mark In
                        sender.Position = _markInPosition.Value;
                    }
                }
                // Handle only Mark Out set: loop to beginning if mark out reached
                else if (!_markInPosition.HasValue && _markOutPosition.HasValue)
                {
                    if (sender.Position >= _markOutPosition.Value)
                    {
                        sender.Position = TimeSpan.Zero;
                    }
                }

                SavePlaybackState();
            });
        }
    }


    private void SavePlaybackState()
    {
        // Only save if the setting is enabled
        if (!Settings.Current.RememberPlaybackPosition) return;

        if (CurrentFile == null || VideoElement.Visibility != Visibility.Visible) return;

        if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter
            && presenter.MediaPlayer != null)
        {
            var session = presenter.MediaPlayer.PlaybackSession;
            if (session == null) return;

            var position = session.Position;
            var duration = session.NaturalDuration;
            var volume = presenter.MediaPlayer.Volume;

            // Use file path as unique identifier
            string fileKey = CurrentFile.Path;

            // Check if video is 95% or more complete
            if (duration > TimeSpan.Zero)
            {
                double percentComplete = position.TotalSeconds / duration.TotalSeconds;

                if (percentComplete >= 0.95)
                {
                    // Reset position to beginning when 95% or more complete
                    position = TimeSpan.Zero;
                }
            }

            // Store as composite value with position, volume, and marks
            var composite = new Windows.Storage.ApplicationDataCompositeValue
            {
                ["Position"] = position.TotalSeconds,
                ["Volume"] = volume,
                ["LastAccessed"] = DateTime.Now.ToBinary()
            };

            // Save mark in/out positions if they exist
            if (_markInPosition.HasValue)
            {
                composite["MarkIn"] = _markInPosition.Value.TotalSeconds;
            }

            if (_markOutPosition.HasValue)
            {
                composite["MarkOut"] = _markOutPosition.Value.TotalSeconds;
            }

            localSettings.Values[$"{PLAYBACK_STATE_KEY}_{fileKey}"] = composite;
        }
    }

    private PlaybackState LoadPlaybackState(StorageFile file)
    {
        // Only load if the setting is enabled
        if (!Settings.Current.RememberPlaybackPosition) return null;

        if (file == null) return null;

        string fileKey = file.Path;

        if (localSettings.Values.TryGetValue($"{PLAYBACK_STATE_KEY}_{fileKey}", out object value)
            && value is Windows.Storage.ApplicationDataCompositeValue composite)
        {
            var state = new PlaybackState();

            if (composite["Position"] is double positionSeconds)
            {
                state.Position = TimeSpan.FromSeconds(positionSeconds);
            }

            if (composite["Volume"] is double volume)
            {
                state.Volume = volume;
            }

            if (composite["MarkIn"] is double markInSeconds)
            {
                state.MarkIn = TimeSpan.FromSeconds(markInSeconds);
            }

            if (composite["MarkOut"] is double markOutSeconds)
            {
                state.MarkOut = TimeSpan.FromSeconds(markOutSeconds);
            }

            return state;
        }

        return null;
    }


    private void ClearPlaybackState(StorageFile file)
    {
        if (file == null) return;

        string fileKey = file.Path;
        string settingKey = $"{PLAYBACK_STATE_KEY}_{fileKey}";

        if (localSettings.Values.ContainsKey(settingKey))
        {
            localSettings.Values.Remove(settingKey);
        }
    }

    #endregion


    #region Mark In/Out

    private TimeSpan? _markInPosition = null;
    private TimeSpan? _markOutPosition = null;
    private bool _isLoopingMarkedSection = false;

    public void TransportControls_MarkIn(object sender, EventArgs e)
    {
        if (VideoElement.Visibility != Visibility.Visible ||
            !(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
            presenter.MediaPlayer == null)
        {
            return;
        }

        var session = presenter.MediaPlayer.PlaybackSession;
        if (session == null) return;

        // If Mark In is set and user clicks again at (or near) the same position, clear it
        if (_markInPosition.HasValue && Math.Abs((_markInPosition.Value - session.Position).TotalSeconds) < 0.1)
        {
            _markInPosition = null;
            ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkIn();
            _isLoopingMarkedSection = false;
            return;
        }

        _markInPosition = session.Position;
        ((CustomMediaTransportControls)VideoElement.TransportControls).SetMarkIn(session.Position);

        // If Mark In is after Mark Out, clear Mark Out
        if (_markOutPosition.HasValue && _markInPosition.Value >= _markOutPosition.Value)
        {
            _markOutPosition = null;
            ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkOut();
            _isLoopingMarkedSection = false;
            return;
        }

        // Enable looping if both marks are set
        if (_markInPosition.HasValue && _markOutPosition.HasValue)
        {
            EnableMarkedSectionLooping();
        }
    }

    public void TransportControls_MarkOut(object sender, EventArgs e)
    {
        if (VideoElement.Visibility != Visibility.Visible ||
            !(VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter) ||
            presenter.MediaPlayer == null)
        {
            return;
        }

        var session = presenter.MediaPlayer.PlaybackSession;
        if (session == null) return;

        // If Mark Out is set and user clicks again at (or near) the same position, clear it
        if (_markOutPosition.HasValue && Math.Abs((_markOutPosition.Value - session.Position).TotalSeconds) < 0.1)
        {
            _markOutPosition = null;
            ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkOut();
            _isLoopingMarkedSection = false;
            return;
        }

        _markOutPosition = session.Position;
        ((CustomMediaTransportControls)VideoElement.TransportControls).SetMarkOut(session.Position);

        // If Mark Out is before Mark In, clear Mark In
        if (_markInPosition.HasValue && _markOutPosition.Value <= _markInPosition.Value)
        {
            _markInPosition = null;
            ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkIn();
            _isLoopingMarkedSection = false;
            return;
        }

        // Enable looping if both marks are set
        if (_markInPosition.HasValue && _markOutPosition.HasValue)
        {
            EnableMarkedSectionLooping();
        }
    }

    public void TransportControls_MarksCleared(object sender, EventArgs e)
    {
        _markInPosition = null;
        _markOutPosition = null;
        _isLoopingMarkedSection = false;

        ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkIn();
        ((CustomMediaTransportControls)VideoElement.TransportControls).ClearMarkOut();

        // Restore original repeat mode behavior
        if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter &&
            presenter.MediaPlayer != null)
        {
            presenter.MediaPlayer.IsLoopingEnabled = (Settings.Current.RepeatMode == RepeatMode.RepeatOne);
        }
    }

    private void EnableMarkedSectionLooping()
    {
        _isLoopingMarkedSection = true;

        if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter &&
            presenter.MediaPlayer != null)
        {
            // Disable the standard MediaPlayer looping since we're handling it manually
            presenter.MediaPlayer.IsLoopingEnabled = false;
        }
    }


    #endregion



    private void ImageElement_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void VisualPlayerPresenter_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        var window = App.Current.ActiveWindow;
        if (window == null) return;

        var appWindow = window.AppWindow;
        if (appWindow == null) return;

        if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
        {
            // Exit fullscreen
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
        }
        else
        {
            // Enter fullscreen
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }
    }




    private async void TransportControls_Liked(object sender, EventArgs e)
    {
        await MessageBox.Show("You liked this video!", Title: "Liked");
    }


    private void TransportControls_RepeatToggled(object sender, RepeatMode e)
    {
        if (VideoElement.FindDescendant("VisualPlayerPresenter") is MediaPlayerPresenter presenter && presenter.MediaPlayer != null)
        {
            // Handle repeat mode changes dynamically
            if (e == RepeatMode.RepeatOne && CurrentFile != null)
            {
                _isSeamlessLooping = true;
                _initialMediaOpened = false; // Reset for new source
                
                // Switch to seamless looping using MediaPlaybackList
                var mediaSource = MediaSource.CreateFromUri(new Uri(CurrentFile.Path));
                var playbackItem = new MediaPlaybackItem(mediaSource);
                var playbackList = new MediaPlaybackList();
                
                // Enable gapless playback for seamless loops
                playbackList.AutoRepeatEnabled = true;
                playbackList.MaxPrefetchTime = TimeSpan.FromSeconds(5);
                playbackList.Items.Add(playbackItem);
                
                // Store current position before switching
                var currentPosition = presenter.MediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;
                var isPlaying = presenter.MediaPlayer.PlaybackSession?.PlaybackState == MediaPlaybackState.Playing;
                
                // Set new source
                presenter.MediaPlayer.Source = playbackList;
                
                // Restore position and playback state after source is loaded
                TypedEventHandler<MediaPlayer, object> handler = null;
                handler = new TypedEventHandler<MediaPlayer, object>((s, args) =>
                {
                    var dq = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;
                    dq.TryEnqueue(() =>
                    {
                        if (s.PlaybackSession != null)
                        {
                            s.PlaybackSession.Position = currentPosition;
                            if (isPlaying)
                            {
                                s.Play();
                            }
                        }
                    });
                    s.MediaOpened -= handler;
                });
                presenter.MediaPlayer.MediaOpened += handler;
            }
            else
            {
                _isSeamlessLooping = false;
                _initialMediaOpened = false; // Reset for new source
                
                // Disable looping or use standard loop for RepeatAll
                // Check if current source is a MediaPlaybackList
                if (presenter.MediaPlayer.Source is MediaPlaybackList && CurrentFile != null)
                {
                    // Switch back to regular MediaSource for non-RepeatOne modes
                    var currentPosition = presenter.MediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;
                    var isPlaying = presenter.MediaPlayer.PlaybackSession?.PlaybackState == MediaPlaybackState.Playing;
                    
                    presenter.MediaPlayer.SetUriSource(new Uri(CurrentFile.Path));
                    presenter.MediaPlayer.IsLoopingEnabled = false;
                    
                    // Restore position and playback state
                    TypedEventHandler<MediaPlayer, object> handler = null;
                    handler = new TypedEventHandler<MediaPlayer, object>((s, args) =>
                    {
                        var dq = App.Current.ActiveWindow?.DispatcherQueue ?? App.DispatcherQueue;
                        dq.TryEnqueue(() =>
                        {
                            if (s.PlaybackSession != null)
                            {
                                s.PlaybackSession.Position = currentPosition;
                                if (isPlaying)
                                {
                                    s.Play();
                                }
                            }
                        });
                        s.MediaOpened -= handler;
                    });
                    presenter.MediaPlayer.MediaOpened += handler;
                }
                else
                {
                    // Already using regular source, just toggle IsLoopingEnabled
                    presenter.MediaPlayer.IsLoopingEnabled = false;
                }
            }
        }
    }



    public bool IsVideoVisible()
    {
        return VideoElement?.Visibility == Visibility.Visible;
    }
}

