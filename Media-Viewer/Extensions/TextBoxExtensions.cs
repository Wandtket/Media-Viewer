using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;

namespace MediaStudio.Extensions
{
    public static class TextBoxExtensions
    {

        private static string Message = "This field is incorrect.";

        /// <summary>
        /// Shows the error message in a tooltip and dismisses the error when a user changes the value.
        /// Remember to only show the error if the control is visible on screen or you'll experience
        /// graphical glitches
        /// </summary>
        /// <param name="textBox"></param>
        /// <param name="AlternateMessage"></param>
        /// <param name="Duration">In Seconds</param>
        public static async Task ShowError(this TextBox textBox, string? AlternateMessage = null, bool ToolTipOnly = false, bool OutlineOnly = false, double Duration = 10)
        {
            //Sometimes the tooltip is stored as a string or an original tooltip
            string? OriginalTip = null;
            ToolTip? OriginalTooltip = null;

            //Don't do anything if the error has already been shown.
            if (ToolTipService.GetToolTip(textBox) == null) { }
            else if (ToolTipService.GetToolTip(textBox).GetType() == typeof(string))
            {
                OriginalTip = (string?)ToolTipService.GetToolTip(textBox);
                if (OriginalTip != null)
                {
                    if (OriginalTip == Message ||
                    OriginalTip == AlternateMessage)
                    {
                        return;
                    }
                }
            }
            else if (ToolTipService.GetToolTip(textBox).GetType() == typeof(ToolTip))
            {
                OriginalTooltip = (ToolTip)ToolTipService.GetToolTip(textBox);
                if (OriginalTooltip.Content != null)
                {
                    if (OriginalTooltip.Content.ToString() == Message ||
                    OriginalTooltip.Content.ToString() == AlternateMessage)
                    {
                        return;
                    }
                }
            }
            else { return; }

            //Original values set by the developer
            Microsoft.UI.Xaml.Media.Brush DefaultBrush = textBox.BorderBrush;
            Thickness DefaultThickness = textBox.BorderThickness;

            //Create a new tooltip to show the user what they did wrong.
            ToolTip toolTip = new ToolTip();

            if (AlternateMessage == null)
            {
                toolTip.Content = Message;
            }
            else
            {
                toolTip.Content = AlternateMessage;
            }

            toolTip.Placement = Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Bottom;
            toolTip.PlacementTarget = textBox;
            ToolTipService.SetToolTip(textBox, toolTip);

            //Change the textbox to be a different border color to show an error.
            if (ToolTipOnly == false)
            {
                textBox.BorderBrush = new SolidColorBrush(Colors.Red);
                textBox.BorderThickness = new Thickness(2, 2, 2, 2);
            }

            //Restore the control after the user makes a control on it.
            textBox.TextChanged += delegate (object sender, TextChangedEventArgs e)
            {
                textBox.BorderBrush = DefaultBrush;
                textBox.BorderThickness = DefaultThickness;
                toolTip.IsOpen = false;

                ToolTipService.SetToolTip(textBox, null);

                if (OriginalTooltip != null)
                {
                    ToolTipService.SetToolTip(textBox, OriginalTooltip);
                }
                else if (OriginalTip != null)
                {
                    ToolTipService.SetToolTip(textBox, OriginalTip);
                }
            };

            //Show the tooltip
            if (OutlineOnly == false)
            {
                toolTip.IsOpen = true;
                await Task.Delay(TimeSpan.FromSeconds(Duration));
                toolTip.IsOpen = false;
            }

        }

    }
}
