using MediaViewer.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Controls
{
    public sealed partial class InfoPanel : UserControl
    {

        public event EventHandler Dismissed;


        public InfoPanel()
        {
            InitializeComponent();
        }


        private void DismissInfoBoxButton_Click(object sender, RoutedEventArgs e)
        {
            Dismissed?.Invoke(this, new EventArgs());
        }

        private async void RichEditBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            RichEditBox box = (RichEditBox)sender;
            box.LostFocus -= RichEditBox_LostFocus;

            box.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");

            if (box.DataContext != null)
            {
                string Comments = (string)box.DataContext;
                box.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, Comments);
            }

            box.LostFocus += RichEditBox_LostFocus;
        }

        private void RichEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            RichEditBox box = (RichEditBox)sender;

            MediaProperties properties = (MediaProperties)this.DataContext;
            box.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out string value);
            properties.Comments = value;
        }

        private void RatingControl_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            RatingControl rater = (RatingControl)sender;
            rater.ValueChanged -= RatingControl_ValueChanged;
            rater.Value = -1;

            if (sender.DataContext != null)
            {
                UInt32? rating = (UInt32)sender.DataContext;

                if (rating == null) { rater.Value = -1; }
                else if (rating == 0) { rater.Value = -1; }
                else if (rating >= 1 && rating <= 12) { rater.Value = 1; }
                else if (rating >= 13 && rating <= 37) { rater.Value = 2; }
                else if (rating >= 38 && rating <= 62) { rater.Value = 3; }
                else if (rating >= 63 && rating <= 87) { rater.Value = 4; }
                else if (rating >= 88) { rater.Value = 5; }
            }

            rater.ValueChanged += RatingControl_ValueChanged;
        }

        private void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            if (this.DataContext != null)
            {
                MediaProperties properties = (MediaProperties)this.DataContext;

                if (sender.Value == -1) { properties.Rating = 0; }
                else if (sender.Value == 1) { properties.Rating = 12; }
                else if (sender.Value == 2) { properties.Rating = 13; }
                else if (sender.Value == 3) { properties.Rating = 38; }
                else if (sender.Value == 4) { properties.Rating = 63; }
                else if (sender.Value == 5) { properties.Rating = 88; }
            }
        }

        private void CopyPath_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (this.DataContext is MediaProperties properties && !string.IsNullOrEmpty(properties.Path))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(properties.Path);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
    }
}
