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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MiniPage : Page
    {
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

            await AudioPlay.LoadAudioFile(File);
        }


    }
}
