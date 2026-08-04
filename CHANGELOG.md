# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] — Unreleased

### Added
- Collapsible events panel in the channel-focus view: browse every event in the file
  and click one to centre the plot on it, keeping the current zoom.

### Changed
- Marker overlay defaults to `Labels` instead of `Lines`.
- Marker labels are staggered across three rows and skip any label that would overlap
  a neighbour, so they stay readable at every zoom level; zooming in reveals more.
- Events are ordered chronologically across all marker streams.

### Fixed
- The overview card's events box scrolls horizontally, so long event values are
  readable instead of being clipped at the card edge.

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
