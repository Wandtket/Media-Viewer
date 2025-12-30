using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace MediaStudio.Converters
{
    internal class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) { return Visibility.Collapsed; }

            bool v = (bool)value;
            if (!v)
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null) { return false; }

            Visibility v = (Visibility)value;
            if (v == Visibility.Collapsed)
            {
                return false;
            }

            return true;
        }
    }
}
