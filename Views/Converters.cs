using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PbRecoil.Views
{
    public class StatusBorderConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(0x30, 0x36, 0x3D));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return ActiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBg = new(Color.FromArgb(0x33, 0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveBg = new(Color.FromArgb(0x33, 0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return ActiveBg;
            }
            return InactiveBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusTextConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveText = new(Color.FromRgb(0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveText = new(Color.FromRgb(0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return ActiveText;
            }
            return InactiveText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
