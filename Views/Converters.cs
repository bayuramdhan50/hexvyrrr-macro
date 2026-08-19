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

    public class SelectedSettingBorderBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBorder = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBorder = new(MediaColor.FromArgb(0x30, 0x30, 0x40, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentIndex && parameter != null && int.TryParse(parameter.ToString(), out int targetIndex))
            {
                return currentIndex == targetIndex ? ActiveBorder : InactiveBorder;
            }
            return InactiveBorder;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class SelectedSettingBgBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBg = new(MediaColor.FromArgb(0x30, 0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBg = new(MediaColor.FromArgb(0x60, 0x0E, 0x15, 0x22));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentIndex && parameter != null && int.TryParse(parameter.ToString(), out int targetIndex))
            {
                return currentIndex == targetIndex ? ActiveBg : InactiveBg;
            }
            return InactiveBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class SelectedSettingForegroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveText = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveText = new(MediaColor.FromRgb(0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentIndex && parameter != null && int.TryParse(parameter.ToString(), out int targetIndex))
            {
                return currentIndex == targetIndex ? ActiveText : InactiveText;
            }
            return InactiveText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class SelectedSettingIndicatorVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentIndex && parameter != null && int.TryParse(parameter.ToString(), out int targetIndex))
            {
                return currentIndex == targetIndex ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            }
            return System.Windows.Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PresetActiveBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromRgb(0x16, 0x1F, 0x2E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentVal && parameter != null && int.TryParse(parameter.ToString(), out int targetVal))
            {
                return currentVal == targetVal ? ActiveBrush : InactiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PresetActiveFgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromRgb(0x09, 0x0E, 0x17));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromRgb(0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentVal && parameter != null && int.TryParse(parameter.ToString(), out int targetVal))
            {
                return currentVal == targetVal ? ActiveBrush : InactiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeActiveBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromArgb(0x40, 0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromArgb(0x20, 0x13, 0x1D, 0x2B));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && value.ToString() == parameter.ToString())
            {
                return ActiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeActiveFgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromRgb(0x8B, 0x94, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && value.ToString() == parameter.ToString())
            {
                return ActiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeActiveBorderConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(MediaColor.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush InactiveBrush = new(MediaColor.FromRgb(0x22, 0x33, 0x4A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && value.ToString() == parameter.ToString())
            {
                return ActiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
