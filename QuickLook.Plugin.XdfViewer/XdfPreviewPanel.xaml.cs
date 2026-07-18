using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using QuickLook.Plugin.XdfViewer.ViewModels;

namespace QuickLook.Plugin.XdfViewer
{
    public partial class XdfPreviewPanel : UserControl
    {
        // Set by the host (Plugin.cs) so the row-click handler can re-read the file on demand
        // for a hi-res "detail" envelope. May be null/empty until SetDataSource is called.
        private string _path;
        private XdfFile _file;
        private XdfOverviewViewModel _vm;

        public XdfPreviewPanel()
        {
            Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
            Resources.Add("InverseBoolToVis", new InverseBooleanToVisibilityConverter());
            InitializeComponent();
        }

        /// <summary>Records the source file path/model so on-demand hi-res reads can re-open it later.</summary>
        public void SetDataSource(string path, XdfFile file)
        {
            _path = path;
            _file = file;
        }

        public void ShowModel(XdfOverviewViewModel vm)
        {
            if (vm == null) { ShowError("Couldn't read this XDF file."); return; }
            _vm = vm;
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

        /// <summary>Applies freshly computed sparkline envelopes (coarse, all-streams pass). Must be
        /// invoked on the UI thread; the host (Plugin.cs) is responsible for the Dispatcher hop.</summary>
        public void ApplyEnvelopes(StreamEnvelope[] envs) => _vm?.ApplyEnvelopes(envs);

        /// <summary>
        /// Row click: toggles the clicked channel's expansion. On first expand of a channel whose
        /// stream has no "detail" envelope yet, kicks off a background hi-res re-read scoped to just
        /// that stream and applies the result once it's back on the UI thread. Cheap to re-click:
        /// once a stream's channels have a Detail, ApplyDetail already populated them, so subsequent
        /// expands of siblings/re-expands of the same channel skip the read entirely.
        /// </summary>
        private void ChannelRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.DataContext is ChannelViewModel cvm)) return;

            cvm.IsExpanded = !cvm.IsExpanded;
            if (!cvm.IsExpanded || cvm.Detail != null) return;
            if (_file == null || string.IsNullOrEmpty(_path)) return;

            var file = _file;
            var path = _path;
            var vm = _vm;
            var streamId = cvm.StreamId;
            var dispatcher = Dispatcher;

            Task.Run(() =>
            {
                try
                {
                    var result = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
                    {
                        OnlyStreamId = streamId,
                        Buckets = XdfReader.ExpandedBuckets,
                        MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStreamHiRes
                    });
                    dispatcher.Invoke(() => vm?.ApplyDetail(result));
                }
                catch (Exception)
                {
                    // A moved/locked/unreadable file must not crash the preview; leave the
                    // channel showing just its coarse sparkline (Detail stays null).
                }
            });
        }

        public void ApplyTheme(QuickLook.Common.Plugin.Themes theme)
        {
            // Set an explicit foreground so text stays readable regardless of the host chrome,
            // and mirror it into a WaveformBrush resource so sparklines (DynamicResource-bound
            // LineBrush) stay legible on both themes too. Cards/gray accents (#888) are
            // theme-neutral and unaffected.
            if (theme == QuickLook.Common.Plugin.Themes.Dark)
            {
                var brush = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
                Foreground = brush;
                Resources["WaveformBrush"] = brush;
            }
            else if (theme == QuickLook.Common.Plugin.Themes.Light)
            {
                var brush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                Foreground = brush;
                Resources["WaveformBrush"] = brush;
            }
            // Themes.None: leave the inherited default (Foreground unset, and sparklines fall
            // back to WaveformSparkline's own built-in default brush since WaveformBrush is
            // never registered as a resource in that case).
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
