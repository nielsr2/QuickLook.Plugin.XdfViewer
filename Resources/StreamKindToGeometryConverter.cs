using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Xdf;

namespace QuickLook.Plugin.XdfViewer.Resources
{
    /// <summary>Maps a <see cref="StreamKind"/> to its icon <see cref="Geometry"/> resource.
    /// Falls back to Icon.Generic for anything unmapped — never null, never throws in a binding path.</summary>
    public sealed class StreamKindToGeometryConverter : IValueConverter
    {
        private static readonly ResourceDictionary Icons = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/QuickLook.Plugin.XdfViewer;component/Resources/StreamIcons.xaml")
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value is StreamKind kind ? "Icon." + kind : "Icon.Generic";
            return Icons.Contains(key) ? Icons[key] : Icons["Icon.Generic"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
