using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace PbRecoil.Views
{
    public class StatusBorderConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromRgb(0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromRgb(0x30, 0x36, 0x3D));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isActive && isActive) ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBg = new(MediaColor.FromArgb(0x28, 0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveBg = new(MediaColor.FromArgb(0x20, 0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isActive && isActive) ? ActiveBg : InactiveBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusTextConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveText = new(MediaColor.FromRgb(0x00, 0xFF, 0x88));
        private static readonly SolidColorBrush InactiveText = new(MediaColor.FromRgb(0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isActive && isActive) ? ActiveText : InactiveText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class FiringColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush FiringBrush = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush IdleBrush   = new(MediaColor.FromRgb(0x30, 0x36, 0x3D));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isFiring && isFiring) ? FiringBrush : IdleBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
