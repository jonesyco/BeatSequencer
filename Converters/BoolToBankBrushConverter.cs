using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BeatSequencer.Converters
{
    public class BoolToBankBrushConverter : IValueConverter
    {
        public Brush FilledBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#43D17A")); // fresh green

        public Brush EmptyBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252A45"));


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool filled = value is bool b && b;
            return filled ? FilledBrush : EmptyBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

