using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
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
        private ChannelViewModel _fullChannel;   // the channel currently shown in the full-window view

        // --- Full-view window state (the single source of truth for zoom/pan) ---
        private long _totalSamples;              // whole-stream sample count
        private double _durationSec;             // whole-stream duration (s), for the time axis
        private double _viewStart;               // left edge of the visible window (samples)
        private double _viewCount;               // width of the visible window (samples)
        private double _renderedStart;           // window the currently-drawn envelope represents
        private double _renderedCount;
        private int _fullReadSeq;                // bumped on every SetView; drops stale async reads
        private DispatcherTimer _readTimer;      // debounces windowed re-reads during a gesture

        // Transient horizontal transform for instant zoom/pan feedback before the crisp read lands.
        private ScaleTransform _waveScale;
        private TranslateTransform _waveTranslate;

        // Drag-to-pan state (on the plot and on the minimap).
        private bool _dragging;
        private double _dragAnchorPx;
        private double _dragAnchorStart;
        private bool _miniDragging;

        private const double MinViewCount = 3.0;     // most zoomed-in: a few samples across the width
        private const double ZoomStep = 1.25;        // per wheel notch
        private const double DotThresholdPx = 6.0;   // must match FullWave.SampleDotThresholdPx
        private const double Inset = 12.0;           // must match FullWave.VerticalInset
        private const int ReadDebounceMs = 110;

        public XdfPreviewPanel()
        {
            Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
            Resources.Add("InverseBoolToVis", new InverseBooleanToVisibilityConverter());
            // Opaque backdrop for the full-window overlay so the cards behind it are hidden.
            // Overridden per-theme in ApplyTheme; this dark default covers Themes.None.
            Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            InitializeComponent();
            // Hook the top-level window so trackpad horizontal scroll (WM_MOUSEHWHEEL, which WPF does
            // not surface as a routed event) can pan the full view.
            Loaded += OnPanelLoaded;
            Unloaded += OnPanelUnloaded;
        }

        private HwndSource _hwndSource;
        private const int WM_MOUSEHWHEEL = 0x020E;

        private void OnPanelLoaded(object sender, RoutedEventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndHook);
        }

        private void OnPanelUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hwndSource != null) { _hwndSource.RemoveHook(WndHook); _hwndSource = null; }
        }

        private IntPtr WndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_MOUSEHWHEEL || _fullChannel == null || FullView.Visibility != Visibility.Visible)
                return IntPtr.Zero;

            long w = IntPtr.Size == 8 ? wParam.ToInt64() : (uint)wParam.ToInt32();
            int delta = (short)((w >> 16) & 0xFFFF);
            if (delta == 0) return IntPtr.Zero;

            // Only act when the pointer is over the plot (DPI-safe via WPF hit coordinates).
            var p = Mouse.GetPosition(PlotCell);
            if (p.X < 0 || p.Y < 0 || p.X > PlotCell.ActualWidth || p.Y > PlotCell.ActualHeight)
                return IntPtr.Zero;

            // Tilt/scroll right (delta > 0) advances the window forward in time.
            double panSamples = (delta / 120.0) * _viewCount * 0.2;
            var v = FullViewMath.Pan(_viewStart, _viewCount, _totalSamples, panSamples);
            SetView(v.Start, v.Count);
            handled = true;
            return IntPtr.Zero;
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

        /// <summary>
        /// Click on the expanded (mid-size) plot: opens the full-window, zoomable/pannable view of
        /// that single channel and kicks off a full-resolution read (cached per channel).
        /// </summary>
        private void ExpandedPlot_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.DataContext is ChannelViewModel cvm)) return;
            e.Handled = true;
            ShowFullChannel(cvm);
        }

        private void ShowFullChannel(ChannelViewModel cvm)
        {
            _fullChannel = cvm;
            FullView.DataContext = cvm;

            long n = cvm.StreamSampleCount ?? (cvm.FullDetail != null ? cvm.FullDetail.BucketCount : 4000);
            _totalSamples = Math.Max(1, n);
            _durationSec = cvm.StreamDurationSeconds ?? 0.0;

            FullViewTitle.Text = string.IsNullOrEmpty(cvm.Unit) ? cvm.Label : cvm.Label + "   " + cvm.Unit;
            FullViewSubtitle.Text = BuildFullSubtitle(cvm);

            // Reset the gain and set up the transient zoom transform on the wave.
            GainSlider.Value = 1.0;
            FullWave.Gain = 1.0;
            GainLabel.Text = "×1";
            _waveScale = new ScaleTransform(1, 1);
            _waveTranslate = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(_waveScale);
            group.Children.Add(_waveTranslate);
            FullWave.RenderTransform = group;

            // Show whatever whole-channel envelope we already have as the initial (fully zoomed-out) frame.
            _renderedStart = 0;
            _renderedCount = _totalSamples;
            FullWave.Envelope = cvm.FullDetail;

            FullView.Visibility = Visibility.Visible;
            // Grab keyboard focus once the overlay is laid out, so Esc/Home/End work right away.
            Dispatcher.BeginInvoke(new Action(() => FullView.Focus()), DispatcherPriority.Input);
            SetView(0, _totalSamples);   // zeroes the window and paints axes/chrome/minimap

            if (cvm.FullDetail == null) LoadFullDetail(cvm);   // whole-channel read (also the scaling + minimap source)
            else UpdateYAxis();
        }

        /// <summary>Reads the whole channel once at full-view resolution; feeds the minimap, the vertical
        /// scale reference, and the fully-zoomed-out frame.</summary>
        private void LoadFullDetail(ChannelViewModel cvm)
        {
            if (_file == null || string.IsNullOrEmpty(_path)) return;

            int buckets = (int)Math.Max(200, Math.Min(_totalSamples, XdfReader.MaxFullBuckets));
            var file = _file;
            var path = _path;
            var streamId = cvm.StreamId;
            var channelIndex = cvm.ChannelIndex;
            var dispatcher = Dispatcher;

            Task.Run(() =>
            {
                try
                {
                    var result = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
                    {
                        OnlyStreamId = streamId,
                        Buckets = buckets,
                        MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStreamFull
                    });
                    dispatcher.Invoke(() =>
                    {
                        var se = (result != null && result.Length > 0) ? result[0] : null;
                        if (se?.Channels != null && channelIndex >= 0 && channelIndex < se.Channels.Length)
                            cvm.SetFullDetail(se.Channels[channelIndex]);
                        if (!ReferenceEquals(_fullChannel, cvm)) return;
                        UpdateYAxis();
                        UpdateMinimap();
                        if (IsFullOut() && cvm.FullDetail != null)
                            SetRenderedEnvelope(cvm.FullDetail, 0, _totalSamples);
                    });
                }
                catch (Exception)
                {
                    // Keep the (empty) full view open rather than crashing; FullDetail stays null.
                }
            });
        }

        private static string BuildFullSubtitle(ChannelViewModel cvm)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("full resolution");
            if (cvm.StreamSampleCount.HasValue)
                sb.Append(" · ").Append(cvm.StreamSampleCount.Value.ToString("N0", ci)).Append(" samples");
            if (cvm.StreamDurationSeconds.HasValue)
                sb.Append(" · ").Append(Xdf.TimeFormat.FormatDuration(cvm.StreamDurationSeconds.Value));
            return sb.ToString();
        }

        private void CloseFullView_Click(object sender, RoutedEventArgs e) => CloseFullView();

        private void CloseFullView()
        {
            _readTimer?.Stop();
            _fullReadSeq++;
            HideCrosshair();
            FullView.Visibility = Visibility.Collapsed;
            FullView.DataContext = null;
            _fullChannel = null;
        }

        /// <summary>Keyboard nav for the full view: Esc closes; Home/End jump to the start/end of the
        /// stream (keeping the current zoom width). Arrow/+/- are intentionally not bound.</summary>
        private void FullView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_fullChannel == null) return;
            switch (e.Key)
            {
                case Key.Escape: CloseFullView(); e.Handled = true; break;
                case Key.Home: SetView(0, _viewCount); e.Handled = true; break;
                case Key.End: SetView(_totalSamples - _viewCount, _viewCount); e.Handled = true; break;
            }
        }

        private void GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FullWave == null) return; // fired during InitializeComponent before the wave exists
            FullWave.Gain = e.NewValue;
            if (GainLabel != null)
                GainLabel.Text = "×" + e.NewValue.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            UpdateYAxis();
        }

        // --- The single choke point: every zoom/pan input funnels through here. ---

        private void SetView(double start, double count)
        {
            if (_fullChannel == null || _totalSamples <= 0) return;

            var w = FullViewMath.Clamp(start, count, _totalSamples, MinViewCount);
            _viewStart = w.Start;
            _viewCount = w.Count;
            _fullReadSeq++;             // invalidate any in-flight read

            UpdateAxesAndChrome();
            ApplyTransientTransform();
            ScheduleRead();
        }

        private bool IsFullOut()
            => _viewStart <= 0.5 && _viewCount >= _totalSamples - 0.5;

        private double ViewportPx => FullWave != null && FullWave.ActualWidth > 0 ? FullWave.ActualWidth : 0;

        private void UpdateAxesAndChrome()
        {
            // Time axis over the visible window (seconds approximated proportionally from sample index).
            if (_durationSec > 0 && _totalSamples > 0)
            {
                TimeAxisEl.StartSeconds = _durationSec * (_viewStart / _totalSamples);
                TimeAxisEl.EndSeconds = _durationSec * ((_viewStart + _viewCount) / _totalSamples);
            }
            else
            {
                TimeAxisEl.StartSeconds = 0;
                TimeAxisEl.EndSeconds = 0;
            }

            // Scrollbar: Value = window start, thumb = window width, track = whole stream.
            FullScrollBar.Maximum = Math.Max(0, _totalSamples - _viewCount);
            FullScrollBar.ViewportSize = _viewCount;
            FullScrollBar.LargeChange = _viewCount;
            FullScrollBar.SmallChange = Math.Max(1, _viewCount * 0.1);
            FullScrollBar.Value = _viewStart;

            UpdateMinimap();
            UpdateBadge();
        }

        private void UpdateBadge()
        {
            double vp = ViewportPx;
            bool sampleAccurate = vp > 0 && _viewCount > 0 && (vp / _viewCount) >= DotThresholdPx;
            if (SampleBadge == null) return;
            if (sampleAccurate)
            {
                int pxPerSample = (int)Math.Round(vp / _viewCount);
                SampleBadgeText.Text = "● sample-accurate · 1 sample ≈ " + pxPerSample + " px";
                SampleBadge.Visibility = Visibility.Visible;
            }
            else
            {
                SampleBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateMinimap()
        {
            if (MiniWave == null || MiniViewport == null) return;
            double w = MiniWave.ActualWidth;
            double h = MiniWave.ActualHeight;
            if (w <= 0 || h <= 0 || _totalSamples <= 0) { MiniViewport.Width = 0; return; }

            // MiniWave has a 4px margin inside the minimap Grid; the viewport rect aligns to its box.
            double left = 4 + w * (_viewStart / _totalSamples);
            double width = Math.Max(2, w * (_viewCount / _totalSamples));
            Canvas.SetLeft(MiniViewport, left);
            Canvas.SetTop(MiniViewport, 4);
            MiniViewport.Width = width;
            MiniViewport.Height = h;
        }

        /// <summary>Maps the currently-drawn envelope (its window) onto the new window with a horizontal
        /// scale+translate, so zoom/pan feels instant before the debounced crisp read arrives.</summary>
        private void ApplyTransientTransform()
        {
            if (_waveScale == null || _renderedCount <= 0 || _viewCount <= 0) return;
            double vp = ViewportPx;
            _waveScale.ScaleX = _renderedCount / _viewCount;
            _waveTranslate.X = vp > 0 ? (_renderedStart - _viewStart) / _viewCount * vp : 0;
        }

        private void ScheduleRead()
        {
            var cvm = _fullChannel;
            if (cvm == null) return;

            // Fully zoomed out: the whole-channel envelope IS the frame; no read needed.
            if (IsFullOut())
            {
                _readTimer?.Stop();
                if (cvm.FullDetail != null && !cvm.FullDetail.IsEmpty)
                    SetRenderedEnvelope(cvm.FullDetail, 0, _totalSamples);
                // else: LoadFullDetail is in flight and will paint the fully-zoomed-out frame.
                return;
            }

            if (_readTimer == null)
            {
                _readTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ReadDebounceMs) };
                _readTimer.Tick += OnReadTimerTick;
            }
            _readTimer.Stop();
            _readTimer.Start();
        }

        private void OnReadTimerTick(object sender, EventArgs e)
        {
            _readTimer.Stop();
            var cvm = _fullChannel;
            if (cvm == null || _file == null || string.IsNullOrEmpty(_path)) return;

            int seq = _fullReadSeq;
            double vs = _viewStart, vc = _viewCount;
            double vp = ViewportPx;
            long start = (long)Math.Floor(vs);
            long count = Math.Max(1, (long)Math.Ceiling(vc));
            int buckets = (int)Math.Max(2, Math.Min(Math.Ceiling(vc), Math.Max(16, vp)));

            var file = _file;
            var path = _path;
            var streamId = cvm.StreamId;
            var channelIndex = cvm.ChannelIndex;
            var dispatcher = Dispatcher;

            Task.Run(() =>
            {
                StreamEnvelope[] result;
                try
                {
                    result = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
                    {
                        OnlyStreamId = streamId,
                        StartSample = start,
                        WindowSampleCount = count,
                        Buckets = buckets,
                        MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStreamFull
                    });
                }
                catch (Exception) { return; }

                dispatcher.Invoke(() =>
                {
                    if (seq != _fullReadSeq || !ReferenceEquals(_fullChannel, cvm)) return; // stale
                    var se = (result != null && result.Length > 0) ? result[0] : null;
                    var env = (se?.Channels != null && channelIndex >= 0 && channelIndex < se.Channels.Length)
                        ? se.Channels[channelIndex] : null;
                    if (env == null || env.IsEmpty) return;
                    ApplyWholeChannelScaling(env);
                    SetRenderedEnvelope(env, vs, vc);
                });
            });
        }

        /// <summary>Pins the windowed envelope's vertical scale to the whole channel so the amplitude
        /// axis and baseline stay put while panning (only the horizontal window changes).</summary>
        private void ApplyWholeChannelScaling(ChannelEnvelope env)
        {
            var full = _fullChannel?.FullDetail;
            if (full == null || full.IsEmpty) return;
            env.Center = full.Center;
            env.DataMin = full.DataMin;
            env.DataMax = full.DataMax;
        }

        private void SetRenderedEnvelope(ChannelEnvelope env, double start, double count)
        {
            _renderedStart = start;
            _renderedCount = count;
            FullWave.Envelope = env;
            if (_waveScale != null) { _waveScale.ScaleX = 1; _waveTranslate.X = 0; }
        }

        // --- Pan/zoom inputs (all route through SetView) ---

        private void PlotCell_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_fullChannel == null) return;
            double vp = ViewportPx;
            if (vp <= 0) return;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            // Mouse: plain vertical wheel pans, Shift+wheel zooms. Trackpad: fine-grained vertical
            // scrolling zooms (no Shift). Shift flips whichever the device does, so both actions are
            // always reachable even if the device heuristic guesses wrong.
            bool zoom = IsMouseWheelNotch(e.Delta) ? shift : !shift;

            if (zoom) ZoomByWheel(e.Delta, e.GetPosition(FullWave).X, vp);
            else PanByWheel(e.Delta);
            e.Handled = true;
        }

        /// <summary>Standard mouse wheels click in ±120 detents; precision-trackpad scrolling reports
        /// finer, non-120 deltas. Used to pick the default action (mouse ⇒ pan, trackpad ⇒ zoom).</summary>
        private static bool IsMouseWheelNotch(int delta)
        {
            int a = Math.Abs(delta);
            return a >= 120 && a % 120 == 0;
        }

        private void ZoomByWheel(int delta, double cursorX, double vp)
        {
            double frac = cursorX / vp;
            if (frac < 0) frac = 0; else if (frac > 1) frac = 1;
            // Scale by delta so mouse notches feel discrete and trackpad scrolling feels smooth.
            double factor = Math.Pow(ZoomStep, -(delta / 120.0)); // scroll up (delta>0) => zoom in
            var w = FullViewMath.ZoomAboutCursor(_viewStart, _viewCount, _totalSamples, frac, factor, MinViewCount);
            SetView(w.Start, w.Count);
        }

        private void PanByWheel(int delta)
        {
            // Scroll up (delta>0) moves the window toward the start.
            double panSamples = -(delta / 120.0) * _viewCount * 0.2;
            var w = FullViewMath.Pan(_viewStart, _viewCount, _totalSamples, panSamples);
            SetView(w.Start, w.Count);
        }

        private void PlotCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_fullChannel == null) return;
            FullView.Focus();   // so Esc/Home/End reach the key handler after a click

            if (e.ClickCount == 2)   // double-click resets to fully zoomed out
            {
                GainSlider.Value = 1.0;
                SetView(0, _totalSamples);
                return;
            }

            _dragging = true;
            _dragAnchorPx = e.GetPosition(FullWave).X;
            _dragAnchorStart = _viewStart;
            PlotCell.CaptureMouse();
            PlotCell.Cursor = Cursors.ScrollWE;
        }

        private void PlotCell_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(FullWave);
            if (!_dragging) { UpdateCrosshair(p); return; }

            HideCrosshair();               // no crosshair while panning
            double vp = ViewportPx;
            if (vp <= 0) return;
            double dxPx = p.X - _dragAnchorPx;
            double dSamples = -dxPx * (_viewCount / vp);   // drag right -> window moves toward start
            SetView(_dragAnchorStart + dSamples, _viewCount);
        }

        private void PlotCell_MouseLeave(object sender, MouseEventArgs e) => HideCrosshair();

        private void HideCrosshair()
        {
            if (CrosshairLayer != null) CrosshairLayer.Visibility = Visibility.Collapsed;
        }

        /// <summary>Positions the hover crosshair at <paramref name="p"/> (FullWave coordinates) and
        /// writes the readout: time (relative to the stream start) plus, when the amplitude scale is
        /// known, the value under the pointer via the inverse vertical map.</summary>
        private void UpdateCrosshair(Point p)
        {
            if (CrosshairLayer == null || _fullChannel == null) { HideCrosshair(); return; }
            double vp = ViewportPx;
            double h = FullWave != null ? FullWave.ActualHeight : 0;
            if (vp <= 0 || h <= 0 || p.X < 0 || p.X > vp || p.Y < 0 || p.Y > h) { HideCrosshair(); return; }

            CrosshairV.X1 = CrosshairV.X2 = p.X; CrosshairV.Y1 = 0; CrosshairV.Y2 = h;
            CrosshairH.Y1 = CrosshairH.Y2 = p.Y; CrosshairH.X1 = 0; CrosshairH.X2 = vp;

            double sample = FullViewMath.SampleAtViewX(_viewStart, _viewCount, p.X / vp);
            double seconds = _totalSamples > 0 ? _durationSec * sample / _totalSamples : 0;
            string text = Xdf.TimeFormat.FormatClock(seconds);

            var env = _fullChannel.FullDetail;
            if (env != null && !env.IsEmpty && env.DataMax > env.DataMin)
            {
                double center = env.Center;
                double halfRange = Math.Max(env.DataMax - center, center - env.DataMin);
                if (halfRange <= 0) halfRange = (env.DataMax - env.DataMin) / 2.0;
                double gain = FullWave.Gain < 1 ? 1 : FullWave.Gain;
                double val = Controls.WaveformSparkline.InvMapYInset(p.Y, center, halfRange, h, gain, Inset);
                text += "   " + FmtAxis(val);
                if (!string.IsNullOrEmpty(_fullChannel.Unit)) text += " " + _fullChannel.Unit;
            }
            CrosshairText.Text = text;

            // Keep the readout chip beside the pointer but inside the plot.
            CrosshairReadout.Measure(new Size(vp, h));
            double rw = CrosshairReadout.DesiredSize.Width, rh = CrosshairReadout.DesiredSize.Height;
            double left = p.X + 10; if (left + rw > vp) left = p.X - 10 - rw; if (left < 0) left = 0;
            double top = p.Y + 12; if (top + rh > h) top = p.Y - 12 - rh; if (top < 0) top = 0;
            Canvas.SetLeft(CrosshairReadout, left);
            Canvas.SetTop(CrosshairReadout, top);

            CrosshairLayer.Visibility = Visibility.Visible;
        }

        private void PlotCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            PlotCell.ReleaseMouseCapture();
            PlotCell.Cursor = Cursors.Arrow;
        }

        // Capture can be revoked without a matching MouseUp (window deactivation, Alt-Tab, a dialog
        // stealing focus mid-drag). Without this, _dragging would stay true and the plot would pan on
        // plain mouse-move afterwards. Reset the drag state whenever capture is lost by any means.
        private void PlotCell_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _dragging = false;
            PlotCell.Cursor = Cursors.Arrow;
        }

        private void PlotCell_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_fullChannel == null) return;
            // Viewport width drives bucket density and the transform; re-fit the current window.
            UpdateAxesAndChrome();
            ApplyTransientTransform();
            ScheduleRead();
        }

        private void FullScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (_fullChannel == null) return;
            SetView(e.NewValue, _viewCount);
        }

        // --- Minimap navigation: click/drag the overview to move the window ---

        private void Mini_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_fullChannel == null) return;
            _miniDragging = true;
            Minimap.CaptureMouse();
            MiniCenterOn(e.GetPosition(MiniWave).X);
            e.Handled = true;
        }

        private void Mini_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_miniDragging) return;
            MiniCenterOn(e.GetPosition(MiniWave).X);
        }

        private void Mini_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_miniDragging) return;
            _miniDragging = false;
            Minimap.ReleaseMouseCapture();
        }

        private void Mini_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _miniDragging = false;
        }

        private void MiniWave_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_fullChannel != null) UpdateMinimap();
        }

        private void MiniCenterOn(double xInMiniWave)
        {
            double w = MiniWave.ActualWidth;
            if (w <= 0) return;
            double frac = xInMiniWave / w;
            if (frac < 0) frac = 0; else if (frac > 1) frac = 1;
            double center = frac * _totalSamples;
            SetView(center - _viewCount / 2.0, _viewCount);
        }

        /// <summary>Refreshes the amplitude-axis labels for the current channel + gain.</summary>
        private void UpdateYAxis()
        {
            if (YMaxLabel == null) return;
            var env = _fullChannel?.FullDetail;
            if (env == null || env.IsEmpty)
            {
                YMaxLabel.Text = YMidLabel.Text = YMinLabel.Text = "";
                return;
            }
            if (env.DataMax <= env.DataMin) // flat channel
            {
                YMidLabel.Text = FmtAxis(env.DataMin);
                YMaxLabel.Text = YMinLabel.Text = "";
                return;
            }

            double gain = FullWave?.Gain ?? 1.0;
            if (gain < 1) gain = 1;
            double center = env.Center;
            double halfRange = Math.Max(env.DataMax - center, center - env.DataMin);
            if (halfRange <= 0) halfRange = (env.DataMax - env.DataMin) / 2.0;
            double half = halfRange / gain;
            YMaxLabel.Text = FmtAxis(center + half);
            YMidLabel.Text = FmtAxis(center);
            YMinLabel.Text = FmtAxis(center - half);
        }

        private static string FmtAxis(double v)
            => v.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);

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
                Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
            else if (theme == QuickLook.Common.Plugin.Themes.Light)
            {
                var brush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                Foreground = brush;
                Resources["WaveformBrush"] = brush;
                Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
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
