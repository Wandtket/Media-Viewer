using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace MediaStudio.Converters
{
    internal class ToggleToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) { return false; }

            bool v = (bool)value;
            if (!v)
            {
                return false;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null) { return false; }

            bool v = (bool)value;
            if (v == false)
            {
                return false;
            }

            return true;
        }
    }
}
