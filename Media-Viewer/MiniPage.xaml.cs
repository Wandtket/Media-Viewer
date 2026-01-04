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
    }
}
