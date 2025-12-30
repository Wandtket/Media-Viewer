using MediaStudio.Extensions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaStudio.Controls.Dialogs
{
    /// <summary>
    /// A simple messagebox to mirror WinForms compatibility.
    /// </summary>
    internal class MessageBox
    {
        public static async Task Show(string Contents, string Title = "", string buttonText = "Ok")
        {
            //If more than one contentdialog is called it will throw an error.
            try
            {
                //Prepare Dialog
                var Dialog = new MessageBoxContent();
                Dialog.XamlRoot = App.Current.ActiveWindow.Content.XamlRoot;
                Dialog.Title = Title;
                Dialog.PrimaryButtonText = buttonText;
                Dialog.DefaultButton = ContentDialogButton.Primary;

                Dialog.Message.Text = Contents;

                //Show
                await Dialog.ShowAsync();
            }
            catch { }
        }
    }

    public sealed partial class MessageBoxContent : ContentDialog
    {
        public MessageBoxContent()
        {
            this.InitializeComponent();
        }
    }
}
