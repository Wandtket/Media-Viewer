using MediaViewer.Extensions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaViewer.Controls.Dialogs
{

    /// <summary>
    /// CommentBox is a larger InputBox and won't allow the enter key to submit the dialog.
    /// </summary>
    internal class ConfirmBox
    {

        public static async Task<ContentDialogResult> Show(string Contents, string Title = "", string primaryText = "Yes", string secondaryText = "Cancel")
        {
            try
            {
                ConfirmBoxContent Dialog = new ConfirmBoxContent();
                Dialog.XamlRoot = App.Current.ActiveWindow.Content.XamlRoot;
                Dialog.Title = Title;
                Dialog.PrimaryButtonText = primaryText;
                Dialog.SecondaryButtonText = secondaryText;
                Dialog.DefaultButton = ContentDialogButton.Secondary;

                Dialog.ConfirmTextBox.Text = Contents;

                //Show
                ContentDialogResult result = await Dialog.ShowAsync();
                return result;
            }
            catch
            {
                return ContentDialogResult.None;
            }
        }
    }


    public sealed partial class ConfirmBoxContent : ContentDialog
    {
        public ConfirmBoxContent()
        {
            this.InitializeComponent();
        }


    }
}
