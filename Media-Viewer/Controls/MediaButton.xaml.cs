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
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaStudio.Controls
{
    public sealed partial class MediaButton : UserControl
    {
        public MediaButton()
        {
            InitializeComponent();
        }

        public void Play()
        {
            this.Visibility = Visibility.Visible;

            PlayPauseIcon.Glyph = "\uE102";
            PulseAnimation.Begin();
        }

        public void Pause()
        {
            this.Visibility = Visibility.Visible;

            PlayPauseIcon.Glyph = "\uE103";
            PulseAnimation.Begin();
        }

        private void PulseAnimation_Completed(object sender, object e)
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
