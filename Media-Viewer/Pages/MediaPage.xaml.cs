using MediaViewer.Controls;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.Search;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MediaPage : Page
{

    public MainPage? ParentPage;

    ObservableCollection<VisualPlayer> Players = App.Current.Players;


    public MediaPage()
    {
        InitializeComponent();
    }


    public void SetSource(StorageFile source, MainPage Page)
    {
        ParentPage = Page;

        VisualPlayer player = new VisualPlayer();
        player.ParentPage = this;

        if (Settings.Current.AutoPlayVideos == true)
        {
            player.VideoElement.AutoPlay = true;
        }

        player.SetSource(source);
        Players.Add(player);

        ParentPage.DataContext = player.Properties;

        LoadDirectory(source);
    }


    private async void LoadDirectory(StorageFile source)
    {
        StorageFolder folder = await source.GetFolder();
        var sortEntry = App.Current.LastSortEntry;

        // Get files and apply the detected sort order
        var Directory = (await folder.GetFilesAsync()).ToList();

        // Apply sorting based on the detected sort order using Windows natural sort
        if (sortEntry != null)
        {
            Directory = sortEntry.Value.PropertyName switch
            {
                "System.ItemNameDisplay" => sortEntry.Value.AscendingOrder
                    ? Directory.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList()
                    : Directory.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList(),

                "System.DateModified" => sortEntry.Value.AscendingOrder
                    ? Directory.OrderBy(f => f.DateCreated).ToList()
                    : Directory.OrderByDescending(f => f.DateCreated).ToList(),

                "System.Size" => sortEntry.Value.AscendingOrder
                    ? Directory.OrderBy(f => f.GetBasicPropertiesAsync().GetAwaiter().GetResult().Size).ToList()
                    : Directory.OrderByDescending(f => f.GetBasicPropertiesAsync().GetAwaiter().GetResult().Size).ToList(),

                "System.FileExtension" => sortEntry.Value.AscendingOrder
                    ? Directory.OrderBy(f => f.FileType, StringComparer.CurrentCultureIgnoreCase).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList()
                    : Directory.OrderByDescending(f => f.FileType, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList(),

                _ => Directory.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList() // Use natural sort as default
            };

            Debug.WriteLine($"Sort: {sortEntry.Value.PropertyName} ({(sortEntry.Value.AscendingOrder == true ? "Ascending" : "Descending")})");
        }
        else
        {
            // Default to natural sort by name if no sort entry detected
            Directory = Directory.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort()).ToList();
        }


        int SourceFileIndex = Directory
            .IndexOf(Directory
            .Where(x => x.Name == source.Name)
            .FirstOrDefault());

        // Find the index of the source player (already added in SetSource)
        int sourcePlayerIndex = Players.Count - 1;

        for (int i = 0; i < Directory.Count; i++)
        {
            StorageFile File = Directory[i];
            MediaType Type = Files.GetMediaType(File.Path);

            if (Type == MediaType.Image || Type == MediaType.Video || Type == MediaType.Gif)
            {
                if (File.Name != source.Name)
                {
                    VisualPlayer player = new VisualPlayer();
                    player.ParentPage = this;

                    if (i == SourceFileIndex - 1 || i == SourceFileIndex + 1)
                    {
                        if (player.MediaLoaded == false)
                        {
                            player.SetSource(File);
                        }
                    }
                    else
                    {
                        player.CurrentFile = File;
                    }

                    // Insert before source file or add after
                    if (i < SourceFileIndex)
                    {
                        Players.Insert(sourcePlayerIndex, player);
                        sourcePlayerIndex++;
                    }
                    else
                    {
                        Players.Add(player);
                    }
                }
            }
        }
    }


    public void BeginMediaPreview(DeviceInformation Device)
    {
        //Player.BeginMediaPreview(Device);
    }


    private void MediaFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        VisualPlayer player = (VisualPlayer)MediaFlipView.SelectedItem;
        VisualPlayer? PreviousPlayer = null;
        VisualPlayer? NextPlayer = null;

        //Check if there's a file before the selected one
        if (MediaFlipView.SelectedIndex > 0)
        {
            PreviousPlayer = MediaFlipView.Items?[MediaFlipView.SelectedIndex - 1] as VisualPlayer;
        }

        //Check if there's a file after the selected one
        if (MediaFlipView.SelectedIndex < MediaFlipView.Items.Count - 1)
        {
            NextPlayer = MediaFlipView.Items?[MediaFlipView.SelectedIndex + 1] as VisualPlayer;
        }


        if (ParentPage != null && player != null)
        {
            // Pause and disable SMTC for previous player
            if (e.RemovedItems?.Count > 0)
            {
                var oldPlayer = e.RemovedItems[0] as VisualPlayer;
                if (oldPlayer?.VideoElement.MediaPlayer != null)
                {
                    oldPlayer.VideoElement.MediaPlayer.Pause();
                    oldPlayer.VideoElement.MediaPlayer.CommandManager.IsEnabled = false;
                }
            }

            ParentPage.DataContext = player.Properties;

            if (player.MediaLoaded == false)
            {
                player.SetSource(player.CurrentFile);
                player.TogglePlayPause();
            }

            App.Current.Player = player;
            App.Current.ActiveWindow.Title = player.CurrentFile.DisplayName
                + player.CurrentFile.FileType;

            ParentPage.FreeMoveButton.IsChecked = player.isFreeMove;
            ParentPage.RotateButton.IsEnabled = player.canRotate;

            // Enable SMTC and play/resume the current player
            if (player.VideoElement.MediaPlayer != null)
            {
                player.VideoElement.MediaPlayer.CommandManager.IsEnabled = true;
            }

            //If there's no previous file don't try to pre-load it.
            if (PreviousPlayer != null)
            {
                if (PreviousPlayer.MediaLoaded == false)
                {
                    PreviousPlayer.SetSource(PreviousPlayer.CurrentFile);
                }

                //Pause and disable SMTC for previous player
                if (PreviousPlayer.VideoElement.MediaPlayer != null)
                {
                    PreviousPlayer.VideoElement.MediaPlayer.Pause();
                    PreviousPlayer.VideoElement.MediaPlayer.CommandManager.IsEnabled = false;
                }
            }

            //If there's no next file don't try to pre-load it.
            if (NextPlayer != null)
            {
                if (NextPlayer.MediaLoaded == false)
                {
                    NextPlayer.SetSource(NextPlayer.CurrentFile);
                }

                //Pause and disable SMTC for next player
                if (NextPlayer.VideoElement.MediaPlayer != null)
                {
                    NextPlayer.VideoElement.MediaPlayer.Pause();
                    NextPlayer.VideoElement.MediaPlayer.CommandManager.IsEnabled = false;
                }
            }
        }

    }

}
