# QuickLook.Plugin.XdfViewer — Design

**Date:** 2026-07-17
**Status:** Approved (design), pending implementation plan
**Author:** Niels Erik Raursø (with Claude)

## 1. Goal

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin that renders a fast,
**high-level overview** of an `.xdf` file (Extensible Data Format — the native
recording format of LabRecorder / Lab Streaming Layer) when the user presses
Space on it in Explorer.

XDF files hold one or more synchronized LSL streams (EEG, EMG, IMU, eye tracking,
markers, motion capture, …) with timestamps, per-stream metadata, and clock-sync
info. The plugin shows the **structure and metadata**, never a dump of the raw
sample data.

Reference plugin (same shape, different format):
[`f1ynng8/QuickLook.Plugin.Hdf5Viewer`](https://github.com/f1ynng8/QuickLook.Plugin.Hdf5Viewer).

## 2. Non-goals (v1 — YAGNI)

- No plotting / waveform rendering of sample data.
- No `.xdfz` (gzip-compressed XDF) support — deferred to a later version.
- No editing, export, or conversion.
- No full sample decode for numeric streams (only string/marker streams are
  sample-decoded, and only for the event preview).

## 3. Requirements

Confirmed with the user during brainstorming:

- **Layout:** hybrid — a one-line file summary bar above a wrap-panel of
  per-stream **cards**.
- **Robust to incomplete recordings:** recordings that crashed before writing a
  `StreamFooter` must still show sample count / duration (via a fallback tally)
  and be flagged **Incomplete**.
- **Per-channel detail:** each data-stream card can expand to show channel
  labels + units/types from the stream header.
- **Marker / event preview:** string-format (irregular) streams can expand to
  show their event values with timestamps.
- **Performance:** must open large (multi-GB) files effectively instantly — never
  read sample payloads except the small marker streams.

## 4. Background

### 4.1 QuickLook plugin contract

Plugins implement `IViewer` (namespace `QuickLook.Common.Plugin`, from the
`QuickLook.Common` submodule):

```csharp
public interface IViewer
{
    int  Priority { get; }
    void Init();
    bool CanHandle(string path);
    void Prepare(string path, ContextObject context);
    void View(string path, ContextObject context);
    void Cleanup();
}
```

`ContextObject` members used here: `object ViewerContent`, `string Title`,
`bool IsBusy`, `System.Windows.Size PreferredSize`, `Themes Theme`,
`double SetPreferredSizeFit(Size, double)`.

The plugin DLL must be named `QuickLook.Plugin.*` and live under the
`QuickLook.Plugin` namespace. It ships as a `.qlplugin` file (a renamed zip built
by `Scripts/pack-zip.ps1`).

### 4.2 XDF binary format (the parts we read)

File = 4-byte magic `XDF:` then a sequence of chunks. Each chunk:

```
[NumLengthBytes: 1 byte ∈ {1,4,8}] [Length: NumLengthBytes bytes, LE] [Tag: 2 bytes LE] [Content]
```

**Critical:** `Length` counts `Tag + Content`; content length = `Length − 2`; the
next chunk begins at `(offset of Tag) + Length`. (Verified empirically against a
real file — this matches pyxdf/libxdf; some prose docs word it misleadingly.)
All binary fields are little-endian; strings are UTF-8.

Chunk tags: `1=FileHeader, 2=StreamHeader, 3=Samples, 4=ClockOffset,
5=Boundary, 6=StreamFooter`.

- **FileHeader** — XML `<info><version>…</version><datetime>…</datetime></info>`.
- **StreamHeader** — `[StreamID:4][XML]`; XML has `<channel_count>`,
  `<nominal_srate>` (0 = irregular), `<channel_format>` ∈
  {int8,int16,int32,int64,float32,double64,string}, plus recommended `<name>`,
  `<type>`, and `<desc><channels><channel><label>/<unit>/<type>…`.
- **StreamFooter** — `[StreamID:4][XML]`; XML has `<first_timestamp>`,
  `<last_timestamp>`, `<sample_count>`, `<measured_srate>`.
- **Samples** — `[StreamID:4][NumSampleBytes:1][NumSamples:varint]` then, per
  sample, `[TimeStampBytes:1 (0 or 8)][ts:double if 8]` followed by
  `channel_count` values. Numeric values are fixed-width LE; a string value is
  `[NumLenBytes:1][Len][UTF-8 bytes]`.
- **ClockOffset / Boundary** — skipped.

## 5. Architecture

Project `QuickLook.Plugin.XdfViewer` — SDK-style (`Microsoft.NET.Sdk`),
`TargetFramework=net462`, `UseWPF=true`, platforms AnyCPU + x64. Identical target
to the HDF5 plugin so it drops into the same QuickLook build/deploy.

```
QuickLook.Plugin.XdfViewer/
├─ QuickLook.Plugin.XdfViewer.sln
├─ QuickLook.Plugin.XdfViewer.csproj
├─ Plugin.cs                       // IViewer implementation
├─ XdfPreviewPanel.xaml / .xaml.cs // WPF UserControl (hybrid cards view)
├─ Xdf/
│  ├─ XdfReader.cs                 // seek-based chunk walker (core)
│  ├─ BinaryReaderExtensions.cs    // ReadVarLen (NumLengthBytes+value) helpers
│  ├─ XdfChunkTag.cs               // tag enum
│  └─ Model/
│     ├─ XdfFile.cs                // version, datetime, IReadOnlyList<XdfStream>
│     ├─ XdfStream.cs              // id, name, type, format, channel_count,
│     │                            //   nominal/measured srate, first/last ts,
│     │                            //   sample_count, IsIncomplete, channels, markers
│     ├─ XdfChannel.cs             // label, unit, type
│     └─ XdfMarker.cs              // timestamp, value
├─ ViewModels/
│  ├─ XdfOverviewViewModel.cs      // file summary + ObservableCollection<StreamCardViewModel>
│  └─ StreamCardViewModel.cs       // formatted per-card display fields
├─ Resources/                      // icon, XAML styles/brushes
├─ Properties/AssemblyInfo.cs
├─ QuickLook.Plugin.Metadata.Base.config
├─ Scripts/
│  ├─ pack-zip.ps1                 // build → .qlplugin
│  └─ update-version.ps1           // pre-build version stamp
└─ QuickLook.Common/               // git submodule + ProjectReference
```

**Unit boundaries.** `XdfReader` knows only bytes → `XdfFile` (no WPF). The
ViewModels format an `XdfFile` for display (no file I/O). The XAML/code-behind
binds ViewModels (no parsing). Each is independently testable; only the
ViewModels touch WPF types.

## 6. Data flow

1. `CanHandle(path)` → `true` when `path` is a file ending `.xdf`
   (case-insensitive). `.xdfz` excluded in v1.
2. `Prepare` → `context.PreferredSize = new Size(750, 550)`.
3. `View` → run `XdfReader.Read(path)` on a background thread → `XdfFile` →
   `XdfOverviewViewModel` → set as the `XdfPreviewPanel.DataContext` →
   `context.ViewerContent = panel`, `context.Title = fileName`,
   `context.IsBusy = false`. The panel shows a light spinner until the model
   arrives.
4. `Init` / `Cleanup` → empty (no shared resources).

`Priority` → `0` (neutral), consistent with the HDF5 plugin.

## 7. Parser design (`XdfReader`)

Single `FileStream` (sequential-scan hint) wrapped in a `BinaryReader`. Verify
the 4-byte magic `XDF:` first; if it does not match, return `null`/empty so
QuickLook falls back to another handler.

Main loop until EOF:

1. Read `NumLengthBytes` (1 byte); read `Length` (that many bytes, LE).
2. Record `tagPos = stream.Position`; read `Tag` (2 bytes LE); compute
   `contentEnd = tagPos + Length`.
3. Dispatch on `Tag`:
   - **FileHeader** → parse `<version>`, `<datetime>`.
   - **StreamHeader** → read `StreamID` (4 bytes); parse XML for name, type,
     `channel_count`, `nominal_srate`, `channel_format`, and channel
     labels/units from `<desc><channels>`.
   - **StreamFooter** → read `StreamID`; parse `first_timestamp`,
     `last_timestamp`, `sample_count`, `measured_srate`.
   - **Samples** → read `StreamID`, then the `NumSamples` varint; add to a
     per-stream running tally. If that stream's `channel_format == string`,
     decode samples (timestamp + string value) into a capped marker list;
     otherwise do not decode values.
   - **ClockOffset / Boundary / unknown** → no work.
4. `stream.Seek(contentEnd, SeekOrigin.Begin)` **unconditionally** — so any field
   we skip or any parse we stop early cannot desync the chunk stream.

**Merge (per StreamID):** prefer the `StreamFooter` for `sample_count` and
duration (`last − first`). If no footer: use the tallied count; estimate duration
as `count / nominal_srate` when `nominal_srate > 0`; mark the stream
`IsIncomplete = true`.

**Ordering guarantee:** `StreamHeader` precedes that stream's `Samples` chunks in
a valid file, so `channel_format` is known before we decide whether to decode
marker values.

**Safety caps & robustness:**
- Marker preview capped at the first **500** events per stream (UI notes "showing
  first 500 of N").
- Channel lists render collapsed by default; the expander uses a **virtualized,
  scrollable** list (the sample Trigno file has **144** channels in one stream).
  Collapse-vs-inline threshold ≈ **32** channels.
- Guard against absurd declared lengths / XML blobs (byte-budget check) so a
  corrupt file can't allocate wildly.
- The whole read is exception-guarded: a malformed chunk stops the scan but
  returns everything parsed so far, with a `IsTruncated` flag on `XdfFile`.

## 8. UI design (`XdfPreviewPanel`)

- **Summary bar:** `fileName · N streams · total duration · XDF vX.Y`
  (total duration = max `last_timestamp` − min `first_timestamp` across streams).
- **Cards** in a `WrapPanel` (or `ItemsControl` + wrap), one per stream:
  - Header: `name · type` (e.g. `Trigno-AUX · IMU`).
  - `channels · format` (e.g. `144 ch · float32`).
  - `rate · sample count` (`512 Hz · 32,512 samples`, or `irregular · 18`).
  - `duration (first–last)`.
  - Expander:
    - data streams → channel labels + units, virtualized list.
    - string/marker streams → event list (`12.34s  "stim_on"`), capped.
  - **Incomplete** streams show a subtle badge.
- **Theming:** honour `ContextObject.Theme` (light/dark) via dynamic resource
  brushes, matching QuickLook's chrome.

## 9. Error handling

- Bad magic → treated as "not mine"; QuickLook falls back. `CanHandle` stays
  extension-based (cheap); the magic check happens in the reader.
- Truncated / corrupt mid-file → render the streams parsed so far plus a
  "file appears truncated" note (`IsTruncated`).
- Unexpected exception anywhere in `View` → caught; panel shows a friendly
  "Couldn't read this XDF file" message rather than propagating (which would
  surface as a QuickLook error).

## 10. Testing strategy (no application launch)

Per the developer's machine policy, the plugin/QuickLook UI is **not** launched
autonomously. Verification is code-level:

- **xUnit project** `QuickLook.Plugin.XdfViewer.Tests` driving `XdfReader`
  directly.
- **Committed synthetic fixtures:** small `.xdf` byte arrays generated in-code
  (a fixture builder) covering: one regular numeric stream; one irregular string
  (marker) stream; a stream with **no footer** (incomplete path); a truncated
  file; a non-XDF file. These commit to the repo so tests run anywhere.
- **Local real-file test (opt-in):** a test that, when the gitignored sample
  `EMG short recording (only ch. 5).xdf` is present, asserts the reader finds the
  expected Trigno stream(s), 144 channels, and plausible sample counts/durations.
  Skipped when the file is absent.
- UI is visually verified by the developer manually loading the built
  `.qlplugin` when they choose to.

## 11. Build & packaging

- `pack-zip.ps1` zips the build output (plugin DLL + `QuickLook.Common.dll` +
  metadata config) and renames to `QuickLook.Plugin.XdfViewer.qlplugin`.
- `update-version.ps1` stamps the assembly version pre-build (mirrors the HDF5
  plugin).
- `QuickLook.Common` referenced as a git submodule; its folder is excluded from
  this project's own compile but kept as a `ProjectReference` (same as the HDF5
  plugin).

## 12. Resolved defaults

- Project/namespace: **`QuickLook.Plugin.XdfViewer`**.
- Preferred window size: **750 × 550**.
- Marker preview cap: **500** events/stream.
- Channel inline-vs-collapse threshold: **≈ 32**.

## 13. Deferred / future work

- `.xdfz` (gzip) support.
- Richer `<desc>` metadata (acquisition device, reference, session/subject).
- Optional tiny sparkline per numeric channel (would require bounded sample
  reads — explicitly out of v1's "no sample data" rule).
