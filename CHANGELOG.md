# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] — 1.2.1

### Added
- Text streams that declare a sample rate are now shown as events, like irregular
  marker streams; before this they showed empty channel rows and their values were never
  read. A new setting, **Show sampled text streams as events** (on by default), switches
  this off. Omitted sample timestamps in such streams are filled in from the sample rate.
- `XDF_CORPUS_DIR`: an opt-in test that checks every recording in a folder for rows,
  labels and waveforms that don't match the data.

### Fixed
- Streams whose header lists no channels (an empty `<desc />`, as written by the g.tec
  Unicorn LSL outlet) or only some of them now show one row per declared channel, with
  waveforms. Before this, the channels section stayed empty. Channels without a label are
  named `ch 1`, `ch 2`, ….
- A header that lists more channels than its `channel_count` no longer adds rows that
  can never get a waveform. A header with no `channel_count` takes the count from its
  channel list, so the stream still shows waveforms.
- A repeated stream header for the same stream no longer shows every channel twice.
- A stream whose footer has no `sample_count` is no longer flagged as incomplete.

## [1.2.0] — 2026-08-12

### Added
- The channel-focus view's events panel has its own search box, so a long event list can
  be narrowed without leaving the plot. Filtering matches event values, like the overview
  search; hidden events drop out of the list entirely, so clicking through the results
  cannot land on one the query is hiding.
- Counter channels are marked with an ⓘ badge in the channel list. A channel like
  OpenSignals' `nSeq` counts samples rather than measuring anything, and previously sat
  in the list looking like data. Counters that wrap are recognized too, as long as they
  wrap slower than one sparkline bucket — faster than that, decimation leaves nothing to
  tell them apart from a full-scale signal.

### Changed
- Channel sparklines join neighbouring buckets whose values don't overlap. A signal that
  barely moves within a bucket but travels across the whole channel — a sample counter
  being the extreme case — drew as a row of sub-pixel slivers, or as nothing at all where
  a bucket held a single sample. Noisy signals are unaffected.

### Fixed
- The light theme now reaches every surface. The crosshair readout, the sample-accurate
  badge, marker value labels, and the minimap with its viewport rectangle were hardcoded
  for dark backgrounds — white text and translucent black chips that washed out or
  vanished on a light panel. Each now carries a light/dark pair (dark is pixel-identical
  to before), and the secondary grays — unit suffixes, event timestamps, and the ⚠/ⓘ
  channel glyphs — darken slightly on light so they stay comfortably readable.
- When the host doesn't state a theme (QuickLook's default), the panel previously assumed
  dark. It now follows the Windows app theme, so a light-mode machine gets a light preview.
  An explicit theme from the host still wins.
- `Home`/`End` in the channel-focus view no longer jump the plot while the caret is in a
  text box; `Esc` there clears the query before a second press closes the view.

## [1.1.0] — 2026-08-05

### Added
- Collapsible events panel in the channel-focus view: browse the file's events and
  click one to centre the plot on it, keeping the current zoom.
- The search box filters events by value alongside channels by label, using the same
  query, so a file whose interesting content is its events is searchable at all. Marker
  cards report "no matching events" when a query clears them, and the box now appears
  for marker-only files. Event times are deliberately not searched — a query like "5"
  matches most timestamps and buries the names the box exists to find.

### Changed
- Marker overlay defaults to `Labels` instead of `Lines`.
- Marker labels are staggered across three rows and skip any label that would overlap
  a neighbour, so they stay readable at every zoom level; zooming in reveals more.
- Events are ordered chronologically across all marker streams.
- Channel sparklines scale with the card instead of staying pinned at 100 px, so a wider
  window grows the waveform rather than the label column. Labels trim, with the full
  name and unit on hover.

### Fixed
- The overview card's events box scrolls horizontally, so long event values are
  readable instead of being clipped at the card edge.
- Trackpad horizontal scroll reaches the card's events box and the full view's events
  panel, whose horizontal scrollbars were previously drag-only.
- Time-axis labels take their precision from the tick step rather than the magnitude of
  the timestamp, so a zoomed-in window past 100 s no longer labels every tick with the
  same whole second.
- Counts of one no longer read "1 events" / "1 channels" on the card or the events
  panel. A truncated count stays plural, since "500+" means at least 500.
- The Markers mode selector now hides alongside the events panel for a channel with
  no usable first timestamp, instead of offering modes over an overlay that suppresses
  itself either way.

## [1.0.0] — 2026-07-20

### Added
- XDF (LabRecorder / LSL) file preview: per-stream overview cards (name, type, channel
  count, sample rate, duration) laid out in a responsive grid.
- Per-stream-type icons (EEG, ECG, EMG, EOG, Respiration, GSR, PPG, Heart Rate,
  Accelerometer, Gyroscope, Magnetometer, Gaze, MoCap, NIRS, Audio, Video, Markers,
  Force, Temperature, and a generic fallback).
- Channel waveform sparklines per stream card (min/max envelope decimation), with
  click-to-expand into a full-window plot.
- Full-window zoom view: mouse-wheel zoom down to individual samples, pan via
  wheel/drag/minimap/scrollbar, a minimap, time and amplitude axes, and a vertical
  gain control.
- Device-aware wheel handling (mouse wheel pans / Shift+wheel zooms; trackpad wheel
  zooms / Shift flips the default) via a `WM_MOUSEHWHEEL` hook.
- Hover crosshair with time + amplitude readout; Esc closes the full view; Home/End
  jump to stream start/end; double-click resets zoom and gain.
- Marker-stream overlay on the full-view plot (Off / Lines / Labels), anchored to the
  selected stream's own start time.
- Channel search box filtering channel rows by label across all cards.
- Flat-channel warning for constant/dead channels.
- Human-readable durations (rolling up to `m`/`h`) and event/marker times shown
  relative to the recording start; footerless files still report a duration.
- Windowed reads with a cached chunk index, so only the visible window is decoded on
  zoom/pan — stays responsive on recordings of a few hundred MB.
