using FFMpegCore;
using MediaViewer.Controls;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using MediaViewer.Models;
using MediaViewer.Pages;
using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;
using SortEntry = MediaViewer.Extensions.SortEntry;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer
{

    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();          
        }


        public new static App Current => (App)Application.Current;

        public Window ActiveWindow { get; set; }


        public VisualPlayer Player { get; set; }

        public ObservableCollection<VisualPlayer> Players { get; set; } = new();

        public SortEntry? LastSortEntry { get; set; }




        public static DispatcherQueue DispatcherQueue { get; private set; }


        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            this.UnhandledException += App_UnhandledException;

            App.Current.ActiveWindow = new Window();
            App.Current.ActiveWindow.Activated += ActiveWindow_Activated;

            DisplayExtensions.Initialize();

            AppActivationArguments ActivationArguments = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            
            if (ActivationArguments.Kind is ExtendedActivationKind.File &&
                ActivationArguments.Data is IFileActivatedEventArgs fileActivatedEventArgs &&
                fileActivatedEventArgs.Files.FirstOrDefault() is IStorageFile storageFile && 
                !Debugger.IsAttached)
            {
                LastSortEntry = await Folders.GetSortOrder(storageFile.Path);

                if (Files.GetMediaType(storageFile.Path) == MediaType.Audio)
                {
                    App.Current.ActiveWindow.Resize(900, 800);
                    App.Current.ActiveWindow.Content = new MiniPage((StorageFile?)storageFile);
                }
                else
                {
                    await App.Current.ActiveWindow.Resize((StorageFile?)storageFile);
                    App.Current.ActiveWindow.Content = new MainPage((StorageFile?)storageFile);
                }
                App.Current.ActiveWindow.Title = storageFile.Name + storageFile.FileType;
            }
            else if (Debugger.IsAttached)
            {
                StorageFile File;
                //File = await StorageFile.GetFileFromPathAsync(@"Z:\TV Shows\Aqua Teen Hunger Force\Season 01\Episode 05. Balloonenstein-5.mkv");
                File = await StorageFile.GetFileFromPathAsync("C:\\Users\\wandt\\OneDrive\\Pictures\\Responses\\ScarletFire.mp3");
                LastSortEntry = await Folders.GetSortOrder(File.Path);

                if (Files.GetMediaType(File.Path) == MediaType.Audio)
                {
                    App.Current.ActiveWindow.Resize(600, 600);
                    App.Current.ActiveWindow.Content = new MiniPage(File);
                }
                else
                {
                    await App.Current.ActiveWindow.Resize(File);
                    App.Current.ActiveWindow.Content = new MainPage(File);
                }

                App.Current.ActiveWindow.Title = File.DisplayName + File.FileType;
                //App.Current.ActiveWindow.Content = new TestPage();
            }
            else
            {
                App.Current.ActiveWindow.Content = new MainPage();
            }


            App.Current.ActiveWindow.MoveToMouseMonitor();
            App.Current.ActiveWindow.Activate();
            //App.Current.ActiveWindow.Maximize();

            App.Current.ActiveWindow.SetBackdrop(Settings.Current.Backdrop);
            App.Current.ActiveWindow.ExtendContentIntoTitleBar();


            LoadIcon();
            LoadFFMPEG();
        }


        public SafeFileHandle Icon;

        private void LoadIcon()
        {
            var hwndd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(App.Current.ActiveWindow));
            Icon = PInvoke.LoadImage(null, @"logo.ico", GDI_IMAGE_TYPE.IMAGE_ICON, 16, 16, IMAGE_FLAGS.LR_LOADFROMFILE);
            PInvoke.SendMessage(hwndd, 0x0080, new WPARAM(0), new LPARAM(Icon.DangerousGetHandle()));
        }



        private void LoadFFMPEG()
        {
            string? Directory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ffmpegPath = Directory + @"\Dependencies\"; // path containing ffmpeg.exe
            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = ffmpegPath
            });
        }


        private void ActiveWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.CodeActivated)
            {
                App.Current.ActiveWindow = (Window)sender;
            }
            else if (args.WindowActivationState == WindowActivationState.Deactivated)
            {

            }
        }



        private async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await ErrorBox.Show(e.Exception);
        }
    
    }
}
