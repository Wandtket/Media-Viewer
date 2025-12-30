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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Controls.Dialogs
{
    /// <summary>
    /// Use ErrorBox in your Exception and qualify control with "this"
    /// </summary>
    internal class ErrorBox
    {
        public static async Task Show(Exception Ex,
            int? ErrorID = null,
            Control? Control = null,
            string Title = "An Error Has Occurred",
            bool SendEmail = false)
        {
            try
            {
                ErrorBoxContent Dialog = new ErrorBoxContent();
                Dialog.XamlRoot = App.Current.ActiveWindow.Content.XamlRoot;

                if (ErrorID != null) { Title = Title + $" (ID: {ErrorID})"; }

                Dialog.ErrorTitle.Text = Title;
                Dialog.ErrorMessage.Text = Ex.Message;

                var result = await Dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && SendEmail == true)
                {
                    string Body = $"Location: {Control?.GetType().FullName}" +
                        "%0A" +
                        "%0A" +
                        Ex.Message;

                    await Email.LaunchAsync("wandtket@gmail.com", Title, Body);
                }
            }
            catch { }
        }

    }

    public sealed partial class ErrorBoxContent : ContentDialog
    {
        public ErrorBoxContent()
        {
            this.InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            // Hide the capture UI if screen capture is not supported.
            bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-screenclip:edit?source=AudubonSuite (Desktop)&clippingMode=Window"));
        }

    }
}
