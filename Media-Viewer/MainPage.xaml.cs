using CommunityToolkit.WinUI;
using MediaViewer.Controls;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using MediaViewer.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Windows.Storage;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private VirtualKey? _heldKey = null;
        private DispatcherTimer _keyHoldTimer;
        private const int SCRUB_INTERVAL_MS = 100;

        public MainPage(StorageFile? File = null)
        {
            this.InitializeComponent();
            Initialize(File);
        }


        private async void Initialize(StorageFile? File = null)
        {
            Loaded += Page_Loaded;
            SizeChanged += MainPage_SizeChanged;

            while (App.Current.ActiveWindow == null) { await Task.Delay(50); }
            while (App.Current.ActiveWindow.Content == null) { await Task.Delay(50); }
            while (App.Current.ActiveWindow.Content.XamlRoot == null) { await Task.Delay(50); }

            PageFrame.Navigate(typeof(MediaPage));

            if (File != null)
            {
                MediaPage Page = ((MediaPage)PageFrame.Content);
                Page.SetSource(File, this);
            }

            if (Settings.Current.AutoHideUI == true) AutoHideUI(true);

            // Initialize the key hold timer for continuous scrubbing
            _keyHoldTimer = new DispatcherTimer();
            _keyHoldTimer.Interval = TimeSpan.FromMilliseconds(SCRUB_INTERVAL_MS);
            _keyHoldTimer.Tick += KeyHoldTimer_Tick;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            App.Current.ActiveWindow.ExtendsContentIntoTitleBar = true;
            App.Current.ActiveWindow.SetTitleBar(CustomDragRegion);
            CustomDragRegion.MinWidth = 188;

            PageFrame.Navigate(typeof(MediaPage));
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (App.Current.ActiveWindow.AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            {
                //ExitFullScreen();
            }

            switch (e.NewSize.Width)
            {
                case < 600:
                    Editbutton.Visibility = Visibility.Collapsed;
                    MediaMenuCommandBar.Visibility = Visibility.Collapsed;
                    FileInfo.Margin = new Thickness(50, 8, 0, 0);
                    break;
                case < 700:
                    Editbutton.Visibility = Visibility.Visible;
                    MediaMenuCommandBar.Visibility = Visibility.Collapsed;
                    FileInfo.Margin = new Thickness(10, 8, 0, 0);
                    break;
                case < 900:
                    Editbutton.Visibility = Visibility.Visible;
                    MediaMenuCommandBar.Visibility = Visibility.Visible;
                    FileInfo.Margin = new Thickness(0, 8, 0, 0);
                    break;
                case > 900:
                    Editbutton.Visibility = Visibility.Visible;
                    MediaMenuCommandBar.Visibility = Visibility.Visible;
                    FileInfo.Margin = new Thickness(0, 8, 0, 0);
                    break;
            }

            //Debug.WriteLine(e.NewSize.Width.ToString());
        }



        private void PageFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            //MediaPage Page = ((MediaPage)PageFrame.Content);

        }


        private async void SaveAsItem_Click(object sender, RoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            await Player.CopyFrameToClipboard();
        }

        private void ResizeItem_Click(object sender, RoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            Player.FitContentToWidth();
        }

        private void MainView_PaneOpening(NavigationView sender, object args)
        {
            PopulateCameraList();

            if (App.Current.ActiveWindow.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                InactivityTimer.Stop();
            }
        }

        private void MainView_PaneOpened(NavigationView sender, object args)
        {
            App.Current.ActiveWindow.SetTitleBar(CustomDragRegion);
        }

        private void MainView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            if (App.Current.ActiveWindow.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                InactivityTimer.Start();
            }
        }

        private void MainView_PaneClosed(NavigationView sender, object args)
        {
            App.Current.ActiveWindow.SetTitleBar(CustomDragRegion);
        }

        private void MainView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected == true)
            {
                PageFrame.Navigate(typeof(SettingsPage));
                ToggleHeaderInfo(false);
            }
        }

        private void MainView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (PageFrame.CanGoBack)
            {
                PageFrame.GoBack();

                ToggleHeaderInfo(true);
            }
        }


        private void ToggleHeaderInfo(bool Show)
        {
            var Visible = Visibility.Collapsed;
            var NavVisibile = NavigationViewBackButtonVisible.Visible;
            bool BackButtonVis = !Show;

            if (Show) { 
                Visible = Visibility.Visible;
                NavVisibile = NavigationViewBackButtonVisible.Collapsed;
                this.SizeChanged += MainPage_SizeChanged;
            }
            else
            {
                this.SizeChanged -= MainPage_SizeChanged;
            }

            MainViewContent.IsPaneOpen = false;

            MediaMenuCommandBar.Visibility = Visible;
            Editbutton.Visibility = Visible;
            MediaControlsCommandBar.Visibility = Visible;
            FileInfo.Visibility = Visible;

            MainView.IsBackEnabled = BackButtonVis;
            MainView.IsBackButtonVisible = NavVisibile;
            MainView.SelectedItem = null;

            
        }


        private async void Coffee_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.buymeacoffee.com/wandtket"));

        }

        private async void Suggestion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Wandtket/Entity-Framework-Toolkit/issues/new"));
        }


        private async void PopulateCameraList()
        {
            var DeviceList = await DeviceInformation.FindAllAsync(MediaDevice.GetVideoCaptureSelector());

            if (DeviceList.Count == 0)
            {
                //tbStatus.Text = "No video capture devices found.";
                return;
            }

            foreach (var device in DeviceList)
            {
                bool NewDevice = true;

                foreach (var item in MainView.MenuItems)
                {
                    if (item.GetType() == typeof(NavigationViewItem))
                    {
                        if (((NavigationViewItem)item).Content.ToString() == device.Name)
                        {
                            NewDevice = false;
                        }
                    }
                }

                if (NewDevice == true)
                {
                    NavigationViewItem NewItem = new NavigationViewItem();
                    NewItem.Content = device.Name;
                    NewItem.Tag = device;
                    NewItem.Tapped += DeviceItem_Tapped;
                    MainView.MenuItems.Add(NewItem);
                }
            }
        }

        private void DeviceItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MediaPage Page = (MediaPage)PageFrame.Content;
            NavigationViewItem Item = (NavigationViewItem)sender;
            DeviceInformation Device = (DeviceInformation)Item.Tag;

            Page.BeginMediaPreview(Device);
        }

        private async void NavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CameraExtensions.aiRstGimbalBootPosR();

            return;

            var file = await Files.SelectFile();

            if (file != null)
            {
                MediaPage Page = (MediaPage)PageFrame.Content;
                Page.SetSource(file, this);
            }
        }




        #region Auto-Hide UI Methods


        private DispatcherTimer InactivityTimer = new();
        private const int InactivityTimeoutMs = 3000;
        private bool HeaderShown = false;

        private void HeaderGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ResetInactivityTimer();
        }

        private void AutoHideUI(bool Enabled)
        {
            if (Enabled) 
            {
                MainView.IsPaneVisible = false;
                PageFrame.Margin = new Thickness(0, 0, 0, 0);
                HeaderGrid.Opacity = 0;
            }
            else 
            {
                PageFrame.Margin = new Thickness(0, 45, 0, 0);
                InactivityTimer.Stop();
                HeaderGrid.Opacity = 1;
                //ShowHeader();
            }

            if (Settings.Current.AutoHideUI == false &&
                App.Current.ActiveWindow.AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            {
                return;
            }

            InactivityTimer = new DispatcherTimer();
            InactivityTimer.Interval = TimeSpan.FromMilliseconds(InactivityTimeoutMs);
            InactivityTimer.Tick += (s, e) => HideHeader();

            //ShowHeader();
            InactivityTimer.Start();
        }

        private void ResetInactivityTimer()
        {
            InactivityTimer.Stop();

            if (Settings.Current.AutoHideUI == false &&
                App.Current.ActiveWindow.AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            {
                PageFrame.Margin = new Thickness(0, 45, 0, 0);
                ShowHeader();
                return;
            }
            else
            {
                PageFrame.Margin = new Thickness(0, 0, 0, 0);
            }

            InactivityTimer = new DispatcherTimer();
            InactivityTimer.Interval = TimeSpan.FromMilliseconds(InactivityTimeoutMs);
            InactivityTimer.Tick += (s, e) => HideHeader();

            if (HeaderShown == false) ShowHeader();
            InactivityTimer.Start();
        }

        private void ShowHeader()
        {
            HeaderShown = true;
            FadeIn.Begin();
            MainView.IsPaneVisible = true;
        }

        private void HideHeader()
        {
            HeaderShown = false;
            InactivityTimer.Stop();

            FadeOut.Begin();
            MainView.IsPaneVisible = false;
        }


        #endregion


        #region Fullscreen Methods

        private void FullscreenButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (App.Current.ActiveWindow.AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            {
                EnterFullScreen();
            }
            else
            {
                ExitFullScreen();
            }
        }


        private void EnterFullScreen()
        {
            App.Current.ActiveWindow.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            WindowButtonMargins.Width = new GridLength(0);
            AutoHideUI(true);
        }

        private void ExitFullScreen()
        {
            App.Current.ActiveWindow.AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            WindowButtonMargins.Width = new GridLength(135);

            if (Settings.Current.AutoHideUI == false)
            {
                PageFrame.Margin = new Thickness(0, 45, 0, 0);
            }
        }


        #endregion

        private void FileInformationButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewContent.IsPaneOpen = !MainViewContent.IsPaneOpen;
        }

        private void InfoPanel_Dismissed(object sender, EventArgs e)
        {
            MainViewContent.IsPaneOpen = false;
        }



        private async void Editbutton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            MediaExtensions.OpenFile(Player.CurrentFile);
        }

        private async void RotateButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            RotateButton.IsEnabled = false;
            await Player.Rotate();
            RotateButton.IsEnabled = true;
        }

        private async void ShareButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            await Player.ShareCurrentFrame();
        }

        private void Directory_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PageFrame.Navigate(typeof(DirectoryPage));
        }


        private void FreeMoveButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;
            Player?.ToggleFreeMove((bool)FreeMoveButton.IsChecked);                   
        }


        private void PIPButton_Checked(object sender, RoutedEventArgs e)
        {
            var presenter = App.Current.ActiveWindow.AppWindow.Presenter as OverlappedPresenter;

            if (PIPButton.IsChecked == true) {
                presenter.IsAlwaysOnTop = true;
                //FullscreenButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                presenter.IsAlwaysOnTop = false;
                //FullscreenButton.Visibility = Visibility.Visible;
            }
        }

        private async void CopyAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;
            
            CopyAccelerator.Invoked -= CopyAccelerator_Invoked;
            await Player?.CopyFrameToClipboard();
            CopyAccelerator.Invoked += CopyAccelerator_Invoked;
            
            NotificationQueue.Show("Frame copied to clipboard...", 2000);
        }

        private async void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            SaveAccelerator.Invoked -= SaveAccelerator_Invoked;
            await Player?.SaveFrameToFile();

            SaveAccelerator.Invoked += SaveAccelerator_Invoked;
        }

        private async void SpaceAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;

            MediaPage? Page = (MediaPage)PageFrame.Content;
            VisualPlayer? Player = (VisualPlayer)Page.MediaFlipView.SelectedItem;

            SpaceAccelerator.Invoked -= SpaceAccelerator_Invoked;
            await Player?.TogglePlayPause();

            SpaceAccelerator.Invoked += SpaceAccelerator_Invoked;
        }


         
        private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Get the current player
            if (PageFrame.Content is not MediaPage page) return;
            if (page.MediaFlipView?.SelectedItem is not VisualPlayer player) return;

            // Handle up/down arrows to adjust scrub amount (only for videos)
            if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                if (!player.IsVideoVisible()) return;

                if (e.Key == VirtualKey.Up)
                {
                    player.IncreaseScrubAmount();
                }
                else
                {
                    player.DecreaseScrubAmount();
                }

                // Display the current scrub amount
                string amountText = player.FormatScrubAmount();
                NotificationQueue.Clear();
                NotificationQueue.Show($"Scrub amount: {amountText}", 1000);

                e.Handled = true;
                return;
            }

            // Only handle left/right arrow keys when a video is visible
            if (!player.IsVideoVisible()) return;

            if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right)
            {
                // Check if this is a new key press
                bool isNewPress = _heldKey != e.Key;

                if (isNewPress)
                {
                    // New key press - perform first scrub immediately
                    _heldKey = e.Key;

                    // Stop any existing timer
                    _keyHoldTimer.Stop();

                    // Perform the scrub
                    player.PerformScrub(e.Key == VirtualKey.Left ? -1 : 1);

                    // Start timer for continuous scrubbing
                    _keyHoldTimer.Start();
                }

                e.Handled = true; // Prevent UI navigation
            }
        }

        private void Page_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if ((char)args.Character == '[')
            {
                if (PageFrame.Content is not MediaPage page) return;
                if (page.MediaFlipView?.SelectedItem is not VisualPlayer player) return;

                player.TransportControls_MarkIn(null, null);
                args.Handled = true;
            }
            else if ((char)args.Character == ']')
            {
                if (PageFrame.Content is not MediaPage page) return;
                if (page.MediaFlipView?.SelectedItem is not VisualPlayer player) return;

                player.TransportControls_MarkOut(null, null);
                args.Handled = true;
            }
            else if ((char)args.Character == '\\')
            {
                if (PageFrame.Content is not MediaPage page) return;
                if (page.MediaFlipView?.SelectedItem is not VisualPlayer player) return;

                player.TransportControls_MarksCleared(null, null);
                args.Handled = true;
            }
        }

        private void Page_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
        {
            // Reset held key state when key is released
            if (e.Key == _heldKey)
            {
                _heldKey = null;
                _keyHoldTimer.Stop();
            }
        }

        private void KeyHoldTimer_Tick(object sender, object e)
        {
            // Get the current player
            if (PageFrame.Content is not MediaPage page) return;
            if (page.MediaFlipView?.SelectedItem is not VisualPlayer player) return;

            // Check if key is still being held and video is visible
            if (_heldKey.HasValue && player.IsVideoVisible())
            {
                // Continue scrubbing at the set pace
                player.PerformScrub(_heldKey == VirtualKey.Left ? -1 : 1);
            }
        }


    }
}
