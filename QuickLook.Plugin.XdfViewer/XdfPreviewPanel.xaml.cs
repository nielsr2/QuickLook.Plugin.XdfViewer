using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.ViewModels;

namespace QuickLook.Plugin.XdfViewer
{
    public partial class XdfPreviewPanel : UserControl
    {
        public XdfPreviewPanel()
        {
            Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
            Resources.Add("InverseBoolToVis", new InverseBooleanToVisibilityConverter());
            InitializeComponent();
        }

        public void ShowModel(XdfOverviewViewModel vm)
        {
            if (vm == null) { ShowError("Couldn't read this XDF file."); return; }
            BusyText.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Collapsed;
            FileNameText.Text = vm.FileName;
            SummaryText.Text = vm.Summary;
            StreamsList.ItemsSource = vm.Streams;
            Root.Visibility = Visibility.Visible;
        }

        public void ShowError(string message)
        {
            BusyText.Visibility = Visibility.Collapsed;
            Root.Visibility = Visibility.Collapsed;
            ErrorText.Text = "Couldn't read this XDF file.\n" + message;
            ErrorText.Visibility = Visibility.Visible;
        }

        public void ApplyTheme(QuickLook.Common.Plugin.Themes theme)
        {
            // Set an explicit foreground so text stays readable regardless of the host chrome.
            // Cards/gray accents (#888) are theme-neutral and unaffected.
            if (theme == QuickLook.Common.Plugin.Themes.Dark)
                Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
            else if (theme == QuickLook.Common.Plugin.Themes.Light)
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            // Themes.None: leave the inherited default.
        }
    }

    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type t, object p, System.Globalization.CultureInfo c)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, System.Type t, object p, System.Globalization.CultureInfo c)
            => Binding.DoNothing;
    }
}
