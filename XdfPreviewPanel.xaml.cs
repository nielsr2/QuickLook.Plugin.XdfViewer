using System;
using System.Collections.Generic;
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
using QuickLook.Plugin.XdfViewer.Settings;

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
        private const int MaxMarkerLines = 4000;     // safety cap against pathological event counts
        // Measurement budget, not a placement budget: bounds how many in-window candidates get built
        // and measured per frame. How many are actually drawn is bounded by collision-skip in
        // MarkerLabelLayout.Place — a 3-row viewport physically holds only ~150 non-overlapping labels,
        // so at 1000 this cap cannot bind before geometry does.
        private const int MaxMarkerLabels = 1000;
        private const int LabelRows = 3;             // stagger rows, filled top-first
        private const double LabelRowHeightPx = 13;  // 9px font + leading
        private const double LabelTopPx = 5;         // row 0's y, just under the arrow heads
        private const double LabelGapPx = 6;         // minimum clearance between labels sharing a row
        private const int MaxLabelChars = 28;        // one huge value must not starve a whole row
        private const double MinCardWidth = 320.0;   // narrowest a stream card should get before dropping a column

        /// <summary>Column count for the stream-card grid, recomputed from the available width so the
        /// cards stretch to fill the preview window. Bound by the ItemsPanel's UniformGrid.</summary>
        public static readonly DependencyProperty CardColumnsProperty = DependencyProperty.Register(
            nameof(CardColumns), typeof(int), typeof(XdfPreviewPanel), new PropertyMetadata(1));

        public int CardColumns
        {
            get => (int)GetValue(CardColumnsProperty);
            set => SetValue(CardColumnsProperty, value);
        }

        // Marker overlay state: the file's events + the display mode (index matches the ComboBox).
        private readonly List<XdfMarker> _fullMarkers = new List<XdfMarker>();
        private int _markerMode = 2;                 // 0 = Off, 1 = Lines, 2 = Labels

        // Events panel (full view): the same markers as the overlay, as clickable rows. Rebuilt per
        // focused channel because the times are relative to that channel's stream first timestamp.
        private readonly List<MarkerViewModel> _fullMarkerVms = new List<MarkerViewModel>();
        private bool _navigatingToEvent;             // re-entrancy guard while clearing SelectedItem
        private bool _fullMarkersTruncated;          // any marker stream hit the per-stream read cap

        // Amber reads clearly over the waveform on both light and dark themes.
        private static readonly Brush MarkerBrush = Frozen(Color.FromRgb(0xE0, 0x8E, 0x2A));
        private static readonly Brush MarkerLabelBg = Frozen(Color.FromArgb(0x99, 0, 0, 0));
        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        // Last theme passed to ApplyTheme, so a color-theme change can re-derive WaveformBrush
        // without the caller having to know/re-pass the current light/dark theme.
        private QuickLook.Common.Plugin.Themes _currentTheme = QuickLook.Common.Plugin.Themes.None;
        // Guards the settings controls (WaveformThemeCombo) against firing their SelectionChanged
        // handler destructively while their initial values are being set from PluginSettings.
        private bool _initializingSettings;

        public XdfPreviewPanel()
        {
            Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
            Resources.Add("InverseBoolToVis", new InverseBooleanToVisibilityConverter());
            Resources.Add("KindToGeometry", new QuickLook.Plugin.XdfViewer.Resources.StreamKindToGeometryConverter());
            // Opaque backdrop for the full-window overlay so the cards behind it are hidden.
            // Overridden per-theme in ApplyTheme; this dark default covers Themes.None.
            Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            // Neutral default so the DynamicResource-bound stream-kind icon isn't left unfilled
            // (and thus invisible) under Themes.None, where ApplyTheme never registers this key.
            Resources["WaveformBrush"] = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            InitializeComponent();
            InitSettingsControls();
            // Hook the top-level window so trackpad horizontal scroll (WM_MOUSEHWHEEL, which WPF does
            // not surface as a routed event) can pan the full view.
            Loaded += OnPanelLoaded;
            Unloaded += OnPanelUnloaded;
        }

        /// <summary>One-time init of the settings popup controls from the persisted PluginSettings,
        /// guarded by <see cref="_initializingSettings"/> so setting SelectedIndex here doesn't
        /// immediately re-save the value it was just loaded from (or trigger a theme refresh before
        /// ApplyTheme has run for the first time).</summary>
        private void InitSettingsControls()
        {
            _initializingSettings = true;
            WaveformThemeCombo.SelectedIndex = (int)PluginSettings.WaveformColorTheme;
            UpdateCustomColorSwatch();
            _initializingSettings = false;
        }

        private void WaveformThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializingSettings || WaveformThemeCombo.SelectedIndex < 0) return;
            PluginSettings.WaveformColorTheme = (WaveformColorTheme)WaveformThemeCombo.SelectedIndex;
            UpdateCustomColorSwatch();
            ApplyTheme(_currentTheme);
        }

        /// <summary>Shows/hides the custom-color swatch button and paints it to preview the current
        /// custom color, whether or not Custom is the active theme.</summary>
        private void UpdateCustomColorSwatch()
        {
            if (CustomColorSwatch == null) return;
            bool isCustom = PluginSettings.WaveformColorTheme == WaveformColorTheme.Custom;
            CustomColorSwatch.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            var c = PluginSettings.CustomWaveformColor;
            CustomColorSwatch.Background = new SolidColorBrush(c);
            CustomColorSwatch.Content = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            CustomColorSwatch.Foreground = new SolidColorBrush(PerceivedBrightness(c) > 0.5
                ? Color.FromRgb(0, 0, 0) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        // Simple perceived-brightness estimate (0..1) so the swatch's own label stays legible
        // against whatever custom color the user picks.
        private static double PerceivedBrightness(Color c)
            => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

        private void CustomColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            var current = PluginSettings.CustomWaveformColor;
            using (var dlg = new System.Windows.Forms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B),
                FullOpen = true
            })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var picked = dlg.Color;
                PluginSettings.CustomWaveformColor = Color.FromArgb(picked.A, picked.R, picked.G, picked.B);
                UpdateCustomColorSwatch();
                ApplyTheme(_currentTheme);
            }
        }

        private HwndSource _hwndSource;
        private const int WM_MOUSEHWHEEL = 0x020E;
        // Device-independent pixels per wheel notch (WHEEL_DELTA = 120) for horizontal scrolling.
        // Three times a ScrollViewer's ~16px line step, matching its own LineRight/LineLeft feel.
        // Trackpads send many sub-notch deltas, which the fractional maths passes through smoothly.
        private const double HScrollPxPerNotch = 48.0;

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
            if (msg != WM_MOUSEHWHEEL) return IntPtr.Zero;

            long w = IntPtr.Size == 8 ? wParam.ToInt64() : (uint)wParam.ToInt32();
            int delta = (short)((w >> 16) & 0xFFFF);
            if (delta == 0) return IntPtr.Zero;

            // Full view open and the pointer over the plot: horizontal wheel pans the time window.
            // (Pointer test is DPI-safe via WPF hit coordinates rather than the message's screen point.)
            if (_fullChannel != null && FullView.Visibility == Visibility.Visible)
            {
                var p = Mouse.GetPosition(PlotCell);
                if (p.X >= 0 && p.Y >= 0 && p.X <= PlotCell.ActualWidth && p.Y <= PlotCell.ActualHeight)
                {
                    // Tilt/scroll right (delta > 0) advances the window forward in time.
                    double panSamples = (delta / 120.0) * _viewCount * 0.2;
                    var v = FullViewMath.Pan(_viewStart, _viewCount, _totalSamples, panSamples);
                    SetView(v.Start, v.Count);
                    handled = true;
                    return IntPtr.Zero;
                }
            }

            // Anywhere else: scroll whatever is horizontally scrollable under the pointer — the
            // card's events box, or the full view's events panel. This fallback is not optional
            // polish: WM_MOUSEHWHEEL has no routed-event equivalent, so a ScrollViewer can never
            // see trackpad horizontal scroll on its own and its scrollbar would be drag-only.
            var sv = HorizontalScrollViewerUnderMouse();
            if (sv == null) return IntPtr.Zero;

            sv.ScrollToHorizontalOffset(sv.HorizontalOffset + (delta / 120.0) * HScrollPxPerNotch);
            handled = true;
            return IntPtr.Zero;
        }

        /// <summary>Nearest ScrollViewer above the pointer that actually has somewhere to scroll
        /// horizontally, or null. Walks the tree by hand because WM_MOUSEHWHEEL surfaces no routed
        /// event — there is no bubbling handler we could attach instead. Skipping ScrollViewers with
        /// no horizontal extent lets the search pass through the page-level one (whose scrolling is
        /// disabled) and reach the card's own.</summary>
        private static ScrollViewer HorizontalScrollViewerUnderMouse()
        {
            var d = Mouse.DirectlyOver as DependencyObject;
            while (d != null)
            {
                if (d is ScrollViewer sv && sv.ScrollableWidth > 0) return sv;
                // Runs/other ContentElements aren't Visuals; VisualTreeHelper.GetParent throws on them.
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return null;
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
            SummaryText.Text = vm.Summary;
            StreamsList.ItemsSource = vm.Streams;

            // Reset the search box and hide it only when there is nothing at all to filter — events
            // count as filterable, so a marker-only file still gets a box.
            SearchBox.Text = "";   // clears any prior filter via TextChanged
            bool anythingToFilter = false;
            foreach (var s in vm.Streams)
                if (s.IsMarkerStream ? s.Markers.Count > 0 : s.Channels.Count > 0)
                { anythingToFilter = true; break; }
            SearchGrid.Visibility = anythingToFilter ? Visibility.Visible : Visibility.Collapsed;

            UpdateCardColumns();
            Root.Visibility = Visibility.Visible;
        }

        private void StreamsList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCardColumns();

        /// <summary>Re-fits the card grid to the current width: as many equal columns as fit at
        /// <see cref="MinCardWidth"/>, never more than there are streams.</summary>
        private void UpdateCardColumns()
        {
            if (StreamsList == null) return;
            int count = _vm?.Streams?.Count ?? 0;
            CardColumns = CardLayout.ColumnsForWidth(StreamsList.ActualWidth, MinCardWidth, count);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text;
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility =
                    string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            ApplySearch(query);
        }

        /// <summary>Hands the query to the view model, which owns the filtering (and is testable
        /// without a WPF host). See <see cref="XdfOverviewViewModel.ApplySearch"/>.</summary>
        private void ApplySearch(string query) => _vm?.ApplySearch(query);

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

            // Collect the file's events (from every marker stream) for the overlay; hide the mode
            // selector when there are none.
            _fullMarkers.Clear();
            if (_file != null)
                foreach (var s in _file.Streams)
                    if (s.IsMarkerStream) _fullMarkers.AddRange(s.Markers);
            // Chronological across streams. A file with two marker streams concatenates out of order,
            // and the label layout is a single left-to-right pass that mis-skips on unordered input.
            _fullMarkers.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            bool hasMarkers = _fullMarkers.Count > 0;

            // Without a usable time zero neither marker surface can place an event: UpdateMarkers
            // suppresses the overlay and EventsList_SelectionChanged refuses to seek. Both the mode
            // selector and the panel therefore hide on the same condition — leaving the selector up
            // would offer Off/Lines/Labels over a plot that draws nothing whichever you picked.
            double panelZero = cvm.StreamFirstTimestamp ?? double.NaN;
            bool canSeek = !double.IsNaN(panelZero);
            bool markersUsable = hasMarkers && canSeek;

            MarkerModeLabel.Visibility = MarkerModeCombo.Visibility =
                markersUsable ? Visibility.Visible : Visibility.Collapsed;

            // Events panel rows, on the focused channel's own time basis (null start makes the view
            // model fall back to raw timestamps, exactly as the overview card does).
            _fullMarkerVms.Clear();
            foreach (var m in _fullMarkers)
                _fullMarkerVms.Add(new MarkerViewModel(m, cvm.StreamFirstTimestamp));
            EventsList.ItemsSource = null;            // force a rebind; the list instance is reused
            EventsList.ItemsSource = _fullMarkerVms;

            // Filter the view rather than collapsing item containers: hidden rows then leave the
            // scroll extent and the keyboard/selection path entirely, so EventsList_SelectionChanged
            // can never seek to an event the query is hiding.
            var view = CollectionViewSource.GetDefaultView(EventsList.ItemsSource);
            if (view != null) view.Filter = o => !((MarkerViewModel)o).IsFilteredOut;

            // Mirror StreamCardViewModel's "500+ events" so a truncated read is never presented as
            // complete, and singularize so a lone event doesn't read as "1 events".
            _fullMarkersTruncated = false;
            if (_file != null)
                foreach (var s in _file.Streams)
                    if (s.IsMarkerStream && s.MarkersTruncated) { _fullMarkersTruncated = true; break; }

            // Each channel opens unfiltered. Setting Text won't raise TextChanged when the box is
            // already empty, so the header/filter pass is made explicitly either way.
            EventsSearchBox.Text = string.Empty;
            ApplyEventsSearch(string.Empty);

            // The panel is meaningless without a usable time zero: with none, every row falls back to
            // the raw absolute LSL clock (near-identical, unreadable values) and every click is a
            // silent no-op via the guard in EventsList_SelectionChanged. Mirrors UpdateMarkers' own
            // suppression for the same reason — a visible-but-inert panel was rejected; the overview
            // card (file-wide recording start) remains the place to read those events instead.
            EventsPanel.Visibility = markersUsable ? Visibility.Visible : Visibility.Collapsed;

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
            _fullMarkers.Clear();
            _fullMarkerVms.Clear();
            if (EventsList != null) EventsList.ItemsSource = null;
            if (MarkerLayer != null) MarkerLayer.Children.Clear();
            FullView.Visibility = Visibility.Collapsed;
            FullView.DataContext = null;
            _fullChannel = null;
        }

        private void EventsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = EventsSearchBox.Text;
            if (EventsSearchPlaceholder != null)
                EventsSearchPlaceholder.Visibility =
                    string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            ApplyEventsSearch(query);
        }

        /// <summary>Hands the query to <see cref="FullViewEventsFilter"/>, which owns the filtering
        /// (and is testable without a WPF host); the view then drops the flagged rows.</summary>
        private void ApplyEventsSearch(string query)
        {
            int matches = FullViewEventsFilter.Apply(_fullMarkerVms, query);
            CollectionViewSource.GetDefaultView(EventsList.ItemsSource)?.Refresh();
            EventsPanelHeader.Text = FullViewEventsFilter.HeaderText(
                matches, _fullMarkerVms.Count, _fullMarkersTruncated, query);
        }

        /// <summary>Keyboard nav for the full view: Esc closes; Home/End jump to the start/end of the
        /// stream (keeping the current zoom width). Arrow/+/- are intentionally not bound.</summary>
        private void FullView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_fullChannel == null) return;

            // This handler tunnels, so it would otherwise swallow keys aimed at the events search box:
            // Home/End belong to the caret while it has focus, and Esc drops the query before it closes
            // the view (a second Esc, on an empty box, falls through and closes as usual).
            if (EventsSearchBox != null && EventsSearchBox.IsKeyboardFocusWithin)
            {
                if (e.Key != Key.Escape) return;                       // Home/End move the caret
                if (!string.IsNullOrEmpty(EventsSearchBox.Text))
                {
                    EventsSearchBox.Clear();
                    e.Handled = true;
                    return;
                }
                // Empty box: fall through, so a second Esc closes the view.
            }

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
            // FullScrollBar.Value (a RangeBase) throws ArgumentException on NaN/Infinity, so a
            // non-finite window here would crash the host preview rather than merely look wrong.
            // The events panel's click handler is the one caller that can reach this with a NaN
            // sample (a marker with no explicit timestamp); reject non-finite input defensively
            // for every caller since this is the single choke point.
            if (double.IsNaN(start) || double.IsNaN(count) ||
                double.IsInfinity(start) || double.IsInfinity(count)) return;

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
            UpdateMarkers();
        }

        private void MarkerModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MarkerModeCombo == null) return;
            _markerMode = MarkerModeCombo.SelectedIndex;   // 0 = Off, 1 = Lines, 2 = Labels
            UpdateMarkers();
        }

        private void EventsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventsChevron == null) return;
            // Expanding pushes the strip left; collapsing lets it slide back to the right edge.
            // U+203A = right chevron, U+2039 = left chevron. Escaped rather than pasted because the
            // two glyphs are near-indistinguishable in source.
            EventsChevron.Text = EventsToggle.IsChecked == true ? "\u203A" : "\u2039";
        }

        /// <summary>Click an event: centre the plot on it, keeping the current zoom. Selection is reset
        /// immediately so clicking the same row twice navigates again (SelectionChanged does not fire
        /// for a re-click of the selected item) and no row is left highlighted.</summary>
        private void EventsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_navigatingToEvent) return;
            var mvm = EventsList.SelectedItem as MarkerViewModel;
            if (mvm == null) return;

            _navigatingToEvent = true;
            try { EventsList.SelectedItem = null; }
            finally { _navigatingToEvent = false; }

            // Without a stream zero or a duration, seconds cannot be mapped to samples. The overlay
            // suppresses itself in the same case, so this stays a no-op rather than mis-seeking.
            if (_fullChannel?.StreamFirstTimestamp == null || _durationSec <= 0 || _totalSamples <= 0) return;

            double sample = _totalSamples * (mvm.RelativeSeconds / _durationSec);
            // A marker with no explicit 8-byte timestamp reads as NaN (renders as a "?" row) and
            // survives to here as a NaN RelativeSeconds; CenterOn does not sanitize it, so guard here.
            if (double.IsNaN(sample)) return;
            var w = FullViewMath.CenterOn(sample, _viewCount, _totalSamples);
            SetView(w.Start, _viewCount);
        }

        /// <summary>Redraws the marker overlay for the visible window in two passes. Pass 1 maps each
        /// event's <c>Timestamp − stream.FirstTimestamp</c> (seconds) to an x via
        /// <see cref="MarkerLayout"/>, draws the line + caret for every in-window event, and (in Labels
        /// mode) builds and measures a label candidate per event, truncating any single huge value so
        /// it can't starve a whole stagger row. Pass 2 places the measured candidates into
        /// <see cref="LabelRows"/> stagger rows via <see cref="MarkerLabelLayout"/>, skipping any label
        /// that would overlap a neighbour rather than shrinking or overlapping it — legibility over
        /// completeness. Labels are added to the Canvas after every line, which (Canvas paints in child
        /// order) is what keeps them drawn on top of the lines rather than under them. Off-window events
        /// are skipped, and the whole overlay is suppressed when the stream has no first timestamp (its
        /// timeline zero is unknown), so events are never mis-placed.</summary>
        private void UpdateMarkers()
        {
            if (MarkerLayer == null) return;
            MarkerLayer.Children.Clear();
            if (_markerMode == 0 || _fullChannel == null || _fullMarkers.Count == 0) return;

            double firstTs = _fullChannel.StreamFirstTimestamp ?? double.NaN;
            if (double.IsNaN(firstTs) || _durationSec <= 0 || _totalSamples <= 0) return;

            double vp = ViewportPx;
            double h = FullWave != null ? FullWave.ActualHeight : 0;
            if (vp <= 0 || h <= 0) return;

            double startSec = _durationSec * (_viewStart / _totalSamples);
            double endSec = _durationSec * ((_viewStart + _viewCount) / _totalSamples);
            bool labels = _markerMode == 2;

            // Pass 1: draw every in-window line, and (in Labels mode) build + measure the candidates.
            var labelBlocks = labels ? new List<TextBlock>() : null;
            var labelXs = labels ? new List<double>() : null;
            var labelWidths = labels ? new List<double>() : null;

            int lines = 0;
            foreach (var m in _fullMarkers)
            {
                double x = MarkerLayout.XForSeconds(m.Timestamp - firstTs, startSec, endSec, vp);
                if (double.IsNaN(x)) continue;
                if (++lines > MaxMarkerLines) break;

                MarkerLayer.Children.Add(new Line
                {
                    X1 = x, X2 = x, Y1 = 0, Y2 = h,
                    Stroke = MarkerBrush, StrokeThickness = 1, Opacity = 0.75
                });
                MarkerLayer.Children.Add(new Polygon
                {
                    Fill = MarkerBrush,
                    Points = new PointCollection { new Point(x - 3, 0), new Point(x + 3, 0), new Point(x, 5) }
                });

                if (labels && labelBlocks.Count < MaxMarkerLabels && !string.IsNullOrEmpty(m.Value))
                {
                    var tb = new TextBlock
                    {
                        Text = TruncateLabel(m.Value), FontSize = 9, Foreground = MarkerBrush,
                        Background = MarkerLabelBg, Padding = new Thickness(2, 0, 2, 0)
                    };
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    labelBlocks.Add(tb);
                    labelXs.Add(x);
                    labelWidths.Add(tb.DesiredSize.Width);
                }
            }

            if (!labels) return;

            // Pass 2: stagger into rows, skipping anything that would overlap. Adding the labels after
            // every line also keeps them on top — a Canvas paints in child order.
            var placements = MarkerLabelLayout.Place(labelXs, labelWidths,
                                                     LabelRows, MaxMarkerLabels, LabelGapPx);
            foreach (var p in placements)
            {
                var tb = labelBlocks[p.Index];
                Canvas.SetLeft(tb, p.X + 3);
                Canvas.SetTop(tb, LabelTopPx + p.Row * LabelRowHeightPx);
                MarkerLayer.Children.Add(tb);
            }
        }

        /// <summary>Clips an event value for the plot overlay so one long value cannot starve a whole
        /// stagger row. The full text stays available in the events panel and the overview card.</summary>
        private static string TruncateLabel(string s)
            => s.Length <= MaxLabelChars ? s : s.Substring(0, MaxLabelChars) + "…";

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
            _currentTheme = theme;   // remembered so a later color-theme change can re-derive WaveformBrush

            // Set an explicit foreground so text stays readable regardless of the host chrome.
            // WaveformBrush is derived separately below: Foreground (plain text legibility) must
            // never be affected by the waveform color-theme choice.
            if (theme == QuickLook.Common.Plugin.Themes.Dark)
            {
                var neutral = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
                Foreground = neutral;
                Resources["WaveformBrush"] = ResolveWaveformBrush(neutral);
                Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
            else if (theme == QuickLook.Common.Plugin.Themes.Light)
            {
                var neutral = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                Foreground = neutral;
                Resources["WaveformBrush"] = ResolveWaveformBrush(neutral);
                Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            }
            else
            {
                // Themes.None: leave Foreground/PanelBackground at their inherited/constructor
                // defaults, as before. But this is this plugin's common real-world runtime state
                // (ContextObject.Theme defaults to None, and not every host explicitly overrides it
                // before View() runs) rather than a rare edge case, so the waveform color-theme
                // picker must still work here — recompute WaveformBrush too, falling back to the
                // same neutral gray the constructor seeds it with when no theme is chosen.
                var neutral = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                Resources["WaveformBrush"] = ResolveWaveformBrush(neutral);
            }
        }

        /// <summary>Mono reproduces today's exact behavior (accent == neutral foreground); the other
        /// themes override the accent color regardless of light/dark.</summary>
        private static Brush ResolveWaveformBrush(SolidColorBrush neutral)
        {
            var accent = PluginSettings.ResolveAccentColor(PluginSettings.WaveformColorTheme);
            return accent.HasValue ? new SolidColorBrush(accent.Value) : neutral;
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
