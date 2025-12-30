using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace MediaStudio.Converters
{
    internal class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool v = (bool)value;
            if (!v)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            Visibility v = (Visibility)value;
            if (v == Visibility.Visible)
            {
                return false;
            }

            return true;
        }
    }
}
