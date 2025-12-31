using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Helpers;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {

        public SettingsPage()
        {
            InitializeComponent();
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //Load Image Editor Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.ImageEditorPath))
            {
                if (File.Exists(Settings.Current.ImageEditorPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.ImageEditorPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.ImageEditorPath);

                    ImageEditorSettingsCard.HeaderIcon = Thumbnail;
                    ImageEditorSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.ImageEditorPath = "";
                }
            }

            //Load GIF Editor Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.GifEditorPath))
            {
                if (File.Exists(Settings.Current.GifEditorPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.GifEditorPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.GifEditorPath);

                    GifEditorSettingsCard.HeaderIcon = Thumbnail;
                    GifEditorSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.GifEditorPath = "";
                }
            }

            //Load Audio Editor Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.AudioEditorPath))
            {
                if (File.Exists(Settings.Current.AudioEditorPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.AudioEditorPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.AudioEditorPath);

                    AudioEditorSettingsCard.HeaderIcon = Thumbnail;
                    AudioEditorSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.AudioEditorPath = "";
                }
            }

            //Load Video Editor Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.VideoEditorPath))
            {
                if (File.Exists(Settings.Current.VideoEditorPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.VideoEditorPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.VideoEditorPath);

                    VideoEditorSettingsCard.HeaderIcon = Thumbnail;
                    VideoEditorSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.VideoEditorPath = "";
                }
            }

            //Load Video Converter Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.VideoConverterPath))
            {
                if (File.Exists(Settings.Current.VideoConverterPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.VideoConverterPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.VideoConverterPath);

                    VideoConverterSettingsCard.HeaderIcon = Thumbnail;
                    VideoConverterSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.VideoConverterPath = "";
                }
            }

            //Load Video Upscaler Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.VideoUpscalerPath))
            {
                if (File.Exists(Settings.Current.VideoUpscalerPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.VideoUpscalerPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.VideoUpscalerPath);

                    VideoUpscalerSettingsCard.HeaderIcon = Thumbnail;
                    VideoUpscalerSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.VideoUpscalerPath = "";
                }
            }

            //Load Image Upscaler Icon & Display Name
            if (!string.IsNullOrEmpty(Settings.Current.VideoUpscalerPath))
            {
                if (File.Exists(Settings.Current.VideoUpscalerPath))
                {
                    var Thumbnail = await Files.GetAppIconAsync(Settings.Current.ImageUpscalerPath);
                    var DisplayName = Files.GetAppDisplayName(Settings.Current.ImageUpscalerPath);

                    ImageUpscalerSettingsCard.HeaderIcon = Thumbnail;
                    ImageUpscalerSettingsCard.Description = DisplayName;
                }
                else
                {
                    Settings.Current.ImageUpscalerPath = "";
                }
            }
        }

        private async void AppFolder_Click(object sender, RoutedEventArgs e)
        {
            string? Directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (Directory != null)
            {
                await Launcher.LaunchFolderPathAsync(Directory);
            }
        }

        private async void LocalFolder_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }



        private async void SelectImageEditingProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                ImageEditorSettingsCard.HeaderIcon = image;
                ImageEditorSettingsCard.Description = displayname;

                Settings.Current.ImageEditorPath = File.Path;
            }
        }

        private async void ClearImageEditor_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will bring up the default Windows 'Open With' dialog when clicking the edit button", 
                "Clear Image Editor?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.ImageEditorPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uEB9F";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                ImageEditorSettingsCard.HeaderIcon = icon;
                ImageEditorSettingsCard.Description = "The program the edit button will default to for photos";
            }
        }


        private async void SelectGIFEditingProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                GifEditorSettingsCard.HeaderIcon = image;
                GifEditorSettingsCard.Description = displayname;

                Settings.Current.GifEditorPath = File.Path;
            }
        }

        private async void ClearGifEditor_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will bring up the default Windows 'Open With' dialog when clicking the edit button",
                "Clear Gif Editor?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.GifEditorPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uF4A9";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                GifEditorSettingsCard.HeaderIcon = icon;
                GifEditorSettingsCard.Description = "The program the edit button will default to for Gifs";
            }
        }



        private async void SelectAudioEditingProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                AudioEditorSettingsCard.HeaderIcon = image;
                AudioEditorSettingsCard.Description = displayname;

                Settings.Current.AudioEditorPath = File.Path;
            }
        }

        private async void ClearAudioEditor_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will bring up the default Windows 'Open With' dialog when clicking the edit button",
                "Clear Audio Editor?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.AudioEditorPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uE8D6";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                AudioEditorSettingsCard.HeaderIcon = icon;
                AudioEditorSettingsCard.Description = "The program the edit button will default to for Audio";
            }
        }


        private async void SelectVideoEditingProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                VideoEditorSettingsCard.HeaderIcon = image;
                VideoEditorSettingsCard.Description = displayname;

                Settings.Current.VideoEditorPath = File.Path;
            }
        }

        private async void ClearVideoEditor_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will bring up the default Windows 'Open With' dialog when clicking the edit button",
                "Clear Video Editor?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.VideoEditorPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uE714";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                VideoEditorSettingsCard.HeaderIcon = icon;
                VideoEditorSettingsCard.Description = "The program the edit button will default to for Videos";
            }
        }


        private async void SelectVideoConvertProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                VideoConverterSettingsCard.HeaderIcon = image;
                VideoConverterSettingsCard.Description = displayname;

                Settings.Current.VideoConverterPath = File.Path;
            }
        }

        private async void ClearVideoConvert_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will hide the option to convert a video.",
                "Clear Video Converter?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.VideoConverterPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uE9A1";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                VideoConverterSettingsCard.HeaderIcon = icon;
                VideoConverterSettingsCard.Description = "The program the convert button will default to for Videos";
            }
        }


        private async void SelectVideoUpscalerProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                VideoUpscalerSettingsCard.HeaderIcon = image;
                VideoUpscalerSettingsCard.Description = displayname;

                Settings.Current.VideoUpscalerPath = File.Path;
            }
        }

        private async void ClearVideoUpscaler_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will hide the option to upscale a video.",
                "Clear Video Upscaler?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.VideoUpscalerPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uE61F";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                VideoUpscalerSettingsCard.HeaderIcon = icon;
                VideoUpscalerSettingsCard.Description = "The program the upscaler button will default to for Videos";
            }
        }


        private async void SelectImageUpscalerProgram_Click(object sender, RoutedEventArgs e)
        {
            var File = await Files.SelectExecutable();

            if (File != null)
            {
                var image = await Files.GetAppIconAsync(File.Path);
                var displayname = Files.GetAppDisplayName(File.Path);

                ImageUpscalerSettingsCard.HeaderIcon = image;
                ImageUpscalerSettingsCard.Description = displayname;

                Settings.Current.ImageUpscalerPath = File.Path;
            }
        }

        private async void ClearImageUpscaler_Click(object sender, RoutedEventArgs e)
        {
            var Result = await ConfirmBox.Show("This will hide the option to upscale an image.",
                "Clear Image Upscaler?", "Clear", "Cancel");

            if (Result == ContentDialogResult.Primary)
            {
                Settings.Current.ImageUpscalerPath = "";
                FontIcon icon = new FontIcon();
                icon.Glyph = "\uE61F";
                icon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                ImageUpscalerSettingsCard.HeaderIcon = icon;
                ImageUpscalerSettingsCard.Description = "The program the upscaler button will default to for Images";
            }
        }







        private void SortBy_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            ObservableCollection<SortBys> source = (ObservableCollection<SortBys>)box.ItemsSource;
            box.SelectedItem = source.Where(x => x.Value == Settings.Current.SortByPreference).FirstOrDefault();
        }

        private void SortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            SortBy by = (SortBy)box.SelectedValue;
            Settings.Current.SortByPreference = by;
        }



        private void SortDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            SortDirection selection = (SortDirection)box.SelectedItem;
            Settings.Current.SortDirectionPreference = selection;
        }



        private void Backdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            Backdrop selection = (Backdrop)box.SelectedItem;
            Settings.Current.Backdrop = selection;
        }

        private void RepeatMode_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            ObservableCollection<RepeatModes> source = (ObservableCollection<RepeatModes>)box.ItemsSource;
            box.SelectedItem = source.Where(x => x.Value == Settings.Current.RepeatMode).FirstOrDefault();
        }

        private void RepeatMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            RepeatMode selection = (RepeatMode)box.SelectedValue;
            Settings.Current.RepeatMode = selection;
        }

    }



    public class Settings : INotifyPropertyChanged
    {


        private static Settings _instance = new Settings();
        public static Settings Current { get { return _instance; } }


        private ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;


        public Backdrop Backdrop
        {
            get { return (Backdrop)(localSettings.Values[backdrop] as int? ?? 0); }
            set { localSettings.Values[backdrop] = (int)value; OnPropertyChanged("Backdrop"); }
        }
        private string backdrop = "backdrop";


        public bool AutoHideUI
        {
            get { return localSettings.Values[autoHideUI] as bool? ?? true; }
            set { localSettings.Values[autoHideUI] = value; OnPropertyChanged("AutoHideUI"); }
        }
        private string autoHideUI = "autoHideUI";


        public RepeatMode RepeatMode
        {
            get { return (RepeatMode)(localSettings.Values[repeatMode] as int? ?? 0); }
            set { localSettings.Values[repeatMode] = (int)value; OnPropertyChanged("RepeatMode"); }
        }
        private string repeatMode = "repeatMode";


        public bool AutoPlayVideos
        {
            get { return localSettings.Values[autoPlayVideos] as bool? ?? true; }
            set { localSettings.Values[autoPlayVideos] = value; OnPropertyChanged("AutoPlayVideos"); }
        }
        private string autoPlayVideos = "autoPlayVideos";


        public bool RememberPlaybackPosition
        {
            get { return localSettings.Values[rememberPlaybackPosition] as bool? ?? true; }
            set { localSettings.Values[rememberPlaybackPosition] = value; OnPropertyChanged("RememberPlaybackPosition"); }
        }
        private string rememberPlaybackPosition = "rememberPlaybackPosition";


        public SortBy SortByPreference
        {
            get { return (SortBy)(localSettings.Values[sortByPreference] as int? ?? 0); }
            set { localSettings.Values[sortByPreference] = (int)value; OnPropertyChanged("SortByPreference"); }
        }
        private string sortByPreference = "sortByPreference";

        public SortDirection SortDirectionPreference
        {
            get { return (SortDirection)(localSettings.Values[sortDirectionPreference] as int? ?? 0); }
            set { localSettings.Values[sortDirectionPreference] = (int)value; OnPropertyChanged("SortDirectionPreference"); }
        }
        private string sortDirectionPreference = "sortDirectionPreference";



        public bool EditEnabled
        {
            get { return localSettings.Values[editEnabled] as bool? ?? true; }
            set { localSettings.Values[editEnabled] = value; OnPropertyChanged("EditEnabled"); }
        }
        private string editEnabled = "editEnabled";


        public string ImageEditorPath
        {
            get { return localSettings.Values[imageEditorPath] as string ?? ""; }
            set { localSettings.Values[imageEditorPath] = value; OnPropertyChanged("ImageEditorPath"); }
        }
        private string imageEditorPath = "imageEditorPath";

        public string GifEditorPath
        {
            get { return localSettings.Values[gifEditorPath] as string ?? ""; }
            set { localSettings.Values[gifEditorPath] = value; OnPropertyChanged("GifEditorPath"); }
        }
        private string gifEditorPath = "gifEditorPath";

        public string AudioEditorPath
        {
            get { return localSettings.Values[audioEditorPath] as string ?? ""; }
            set { localSettings.Values[audioEditorPath] = value; OnPropertyChanged("AudioEditorPath"); }
        }
        private string audioEditorPath = "audioEditorPath";

        public string VideoEditorPath
        {
            get { return localSettings.Values[videoEditorPath] as string ?? ""; }
            set { localSettings.Values[videoEditorPath] = value; OnPropertyChanged("VideoEditorPath"); }
        }
        private string videoEditorPath = "videoEditorPath";

        public string VideoConverterPath
        {
            get { return localSettings.Values[videoConverterPath] as string ?? ""; }
            set { localSettings.Values[videoConverterPath] = value; OnPropertyChanged("VideoConverterPath"); }
        }
        private string videoConverterPath = "videoConverterPath";


        public string VideoUpscalerPath
        {
            get { return localSettings.Values[videoUpscalerPath] as string ?? ""; }
            set { localSettings.Values[videoUpscalerPath] = value; OnPropertyChanged("VideoUpscalerPath"); }
        }
        private string videoUpscalerPath = "videoUpscalerPath";

        public string ImageUpscalerPath
        {
            get { return localSettings.Values[imageUpscalerPath] as string ?? ""; }
            set { localSettings.Values[imageUpscalerPath] = value; OnPropertyChanged("ImageUpscalerPath"); }
        }
        private string imageUpscalerPath = "imageUpscalerPath";



        public bool Promotion
        {
            get { return localSettings.Values[promotion] as bool? ?? true; }
            set { localSettings.Values[promotion] = value; OnPropertyChanged("Promotion"); }
        }
        private string promotion = "promotion";


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string Name)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        }

    }


}
