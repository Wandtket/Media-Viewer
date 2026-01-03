using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.Storage;

namespace MediaViewer
{
    public sealed partial class MiniPage : Page
    {
        private bool isNarrowView = false;
        private bool isTransportNarrow = false;

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

            //await AudioPlay.LoadAudioFile(File);
        }

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
            
            PlaylistColumn.Width = new GridLength(1, GridUnitType.Star);
            AlbumArtColumn.Width = new GridLength(1, GridUnitType.Auto);
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
            RightControls.Visibility = Visibility.Visible;
            
            TransportTitle.Visibility = Visibility.Visible;
            ProgressGrid.Margin = new Thickness(0);
        }

        private void SetupNarrowTransport()
        {
            // Narrow transport: 2 rows, timeline on top spanning full width, controls below
            TransportGrid.RowDefinitions[0].Height = GridLength.Auto;
            TransportGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            
            LeftControls.SetValue(Grid.RowProperty, 1);
            LeftControls.SetValue(Grid.ColumnProperty, 0);
            LeftControls.SetValue(Grid.ColumnSpanProperty, 3);
            LeftControls.HorizontalAlignment = HorizontalAlignment.Center;
            
            CenterControls.SetValue(Grid.RowProperty, 0);
            CenterControls.SetValue(Grid.ColumnProperty, 0);
            CenterControls.SetValue(Grid.ColumnSpanProperty, 3);
            
            RightControls.Visibility = Visibility.Collapsed;
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
    }
}
