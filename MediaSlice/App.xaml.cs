using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MediaSlice
{
    public partial class App : System.Windows.Application
    {
    }

    public class IndexToColorConverter : IValueConverter
    {
        private static readonly System.Windows.Media.Color[] Colors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x23, 0xFF, 0xAD), // #23FFAD
            System.Windows.Media.Color.FromRgb(0x1A, 0xE6, 0xB8), 
            System.Windows.Media.Color.FromRgb(0x12, 0xCD, 0xC3),
            System.Windows.Media.Color.FromRgb(0x0A, 0xB4, 0xCE),
            System.Windows.Media.Color.FromRgb(0x00, 0x9C, 0xD9),
            System.Windows.Media.Color.FromRgb(0x00, 0x84, 0xE4),
            System.Windows.Media.Color.FromRgb(0x00, 0x6C, 0xEF),
            System.Windows.Media.Color.FromRgb(0x54, 0x54, 0xFF),
            System.Windows.Media.Color.FromRgb(0x88, 0x3C, 0xFF),
            System.Windows.Media.Color.FromRgb(0xBC, 0x24, 0xFA)
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool active && active)
            {
                int index = parameter is int i ? i : 0;
                var color = Colors[index % Colors.Length];
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x32, 0x50));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ProgressSegmentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x32, 0x50));
            
            var presenter = values[0] as System.Windows.Controls.ContentPresenter;
            double progress = values[1] is double p ? p : 0;
            
            if (presenter == null) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x32, 0x50));
            
            int index = GetIndex(presenter);
            int totalSegments = 50;
            double segmentThreshold = (index + 1) * 100.0 / totalSegments;
            
            if (progress >= segmentThreshold - (100.0 / totalSegments))
            {
                int colorIndex = (int)(progress / 100.0 * 9);
                var colors = new[]
                {
                    System.Windows.Media.Color.FromRgb(0x23, 0xFF, 0xAD),
                    System.Windows.Media.Color.FromRgb(0x1A, 0xE6, 0xB8),
                    System.Windows.Media.Color.FromRgb(0x12, 0xCD, 0xC3),
                    System.Windows.Media.Color.FromRgb(0x0A, 0xB4, 0xCE),
                    System.Windows.Media.Color.FromRgb(0x00, 0x9C, 0xD9),
                    System.Windows.Media.Color.FromRgb(0x00, 0x84, 0xE4),
                    System.Windows.Media.Color.FromRgb(0x00, 0x6C, 0xEF),
                    System.Windows.Media.Color.FromRgb(0x54, 0x54, 0xFF),
                    System.Windows.Media.Color.FromRgb(0x88, 0x3C, 0xFF),
                    System.Windows.Media.Color.FromRgb(0xBC, 0x24, 0xFA)
                };
                return new SolidColorBrush(colors[Math.Min(colorIndex, 9)]);
            }
            
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x32, 0x50));
        }

        private int GetIndex(System.Windows.Controls.ContentPresenter presenter)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(presenter) as System.Windows.Controls.ItemsControl;
            if (parent != null)
            {
                return parent.ItemContainerGenerator.IndexFromContainer(presenter);
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ProgressHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 8.0;
            
            var presenter = values[0] as System.Windows.Controls.ContentPresenter;
            double progress = values[1] is double p ? p : 0;
            
            if (presenter == null) return 8.0;
            
            int index = GetIndex(presenter);
            int totalSegments = 50;
            double segmentThreshold = (index + 1) * 100.0 / totalSegments;
            
            if (progress >= segmentThreshold - (100.0 / totalSegments))
                return 20.0;
            
            return 8.0;
        }

        private int GetIndex(System.Windows.Controls.ContentPresenter presenter)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(presenter) as System.Windows.Controls.ItemsControl;
            if (parent != null)
                return parent.ItemContainerGenerator.IndexFromContainer(presenter);
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}