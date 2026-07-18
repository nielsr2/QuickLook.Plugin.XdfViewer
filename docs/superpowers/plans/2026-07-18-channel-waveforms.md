# Channel Waveform Preview — Implementation Plan (detailed)

> Executable version of `~/.claude/plans/noble-roaming-tide.md`. Continues the shipped
> plugin's TDD conventions. Tasks reordered for build dependencies (model before decimator).

**Goal:** inline min/max **sparkline** per numeric channel + **click-to-expand** a larger static
decimated plot, loaded as a **bounded** second pass that never blocks the instant overview.

## Global Constraints
- Target `net462`, WPF, namespace `QuickLook.Plugin.XdfViewer`. **Plugin csproj MUST keep zero
  `<PackageReference>`** — hand-rolled WPF drawing only (`System.Windows.Media`).
- Model + view models stay **WPF-free** (envelopes are `float[]`, never WPF `Geometry`);
  `ObservableCollection` is the only allowed WPF-ish type in VMs.
- **Never decode numeric sample values in `XdfReader.Read`** — the instant overview path is
  byte-for-byte untouched. All numeric decode lives in the new `FillEnvelopes`.
- XDF sample layout: `[tsMarker:1 (0 or 8)][ts:double if 8]` then `channel_count` fixed-width LE
  values. Sample byte-size varies → **stride at whole-Samples-chunk granularity** via the
  existing `stream.Seek(contentEnd)` resync; never seek mid-chunk.
- Numeric `channel_format`: int8/int16/int32/int64/float32/double64 (string = marker, not a waveform).
- TDD: RED→GREEN, synthetic fixtures, output pristine. **Never launch QuickLook** — rendering is
  manual-verify; all math is unit-tested. Commits: explicit paths, no `git add -A`; never commit
  `.superpowers/`, `*.xdf`, `*.qlplugin`.
- Canonical: `dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Debug`;
  `dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug`.

---

### Task 12: Fixture — write real numeric samples

**Files:** Modify `QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilder.cs`; Test `…Tests/XdfFixtureBuilderTests.cs`.
**Produces:** `XdfFixtureBuilder SamplesNumeric(uint id, string format, int channelCount, bool withTimestamps, params double[][] samples)` — emits one Samples chunk with real fixed-width LE values. Multi-chunk = call it repeatedly with the same id.

- [ ] **Step 1 — failing test** (`XdfFixtureBuilderTests`): build `new XdfFixtureBuilder().SamplesNumeric(1,"int16",2,false, new[]{new double[]{1,-2}, new double[]{3,-4}})`, then manually decode the produced bytes (skip 4-byte magic; the chunk = NumLenBytes+len+tag(3)+streamId(4)+numSamples varint+samples) with a `BinaryReader` and assert the two samples' int16 values round-trip. Also assert a `float32` sample round-trips.
- [ ] **Step 2** — run, confirm RED (method missing).
- [ ] **Step 3 — implement** in `XdfFixtureBuilder`:
```csharp
        public XdfFixtureBuilder SamplesNumeric(uint id, string format, int channelCount,
            bool withTimestamps, params double[][] samples)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, samples.Length);
                foreach (var sample in samples)
                {
                    if (withTimestamps) { w.Write((byte)8); w.Write(0.0); } else w.Write((byte)0);
                    for (int c = 0; c < channelCount; c++) WriteNumericValue(w, format, sample[c]);
                }
                Chunk(3, m.ToArray());
            }
            return this;
        }

        private static void WriteNumericValue(BinaryWriter w, string format, double v)
        {
            switch (format)
            {
                case "int8":     w.Write((sbyte)v);  break;
                case "int16":    w.Write((short)v);  break;
                case "int32":    w.Write((int)v);    break;
                case "int64":    w.Write((long)v);   break;
                case "float32":  w.Write((float)v);  break;
                case "double64": w.Write(v);         break;
                default: throw new System.ArgumentException($"unsupported format {format}");
            }
        }
```
- [ ] **Step 4** — run `dotnet test …Tests --filter XdfFixtureBuilderTests`, GREEN. Full suite. **Step 5** — commit `XdfFixtureBuilder.cs` + `XdfFixtureBuilderTests.cs`.

---

### Task 13: Format → (width, reader) map

**Files:** Create `QuickLook.Plugin.XdfViewer/Xdf/XdfSampleFormat.cs`; Test `…Tests/XdfSampleFormatTests.cs`.
**Produces:** `internal static bool XdfSampleFormat.TryGetNumeric(string format, out int width, out Func<BinaryReader,double> read)`.

- [ ] **Step 1 — failing test:** for each format assert width + that a known LE byte sequence decodes to the expected double (e.g. `int16` bytes `0x14,0x3c` → `0x3c14`; `int8` `0xFF` → `-1`; `float32` bytes of `1.5f` → `1.5`). Assert `"string"`, `""`, `null`, `"foo"` → returns false.
- [ ] **Step 2** — RED. **Step 3 — implement:**
```csharp
using System;
using System.IO;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal static class XdfSampleFormat
    {
        public static bool TryGetNumeric(string format, out int width, out Func<BinaryReader, double> read)
        {
            switch (format?.Trim().ToLowerInvariant())
            {
                case "int8":     width = 1; read = r => r.ReadSByte();  return true;
                case "int16":    width = 2; read = r => r.ReadInt16();  return true;
                case "int32":    width = 4; read = r => r.ReadInt32();  return true;
                case "int64":    width = 8; read = r => r.ReadInt64();  return true;
                case "float32":  width = 4; read = r => r.ReadSingle(); return true;
                case "double64": width = 8; read = r => r.ReadDouble(); return true;
                default:         width = 0; read = null;               return false;
            }
        }
    }
}
```
- [ ] **Step 4** — GREEN + full suite. **Step 5** — commit.

---

### Task 14: Model — `ChannelEnvelope` + `XdfChannel.Envelope`

**Files:** Create `QuickLook.Plugin.XdfViewer/Xdf/Model/ChannelEnvelope.cs`, `.../Model/StreamEnvelope.cs`; Modify `Xdf/Model/XdfChannel.cs`; Test `…Tests/ChannelEnvelopeTests.cs`.
**Produces:** WPF-free POCOs. `ChannelEnvelope { float[] Min,Max; bool[] Filled; int BucketCount; float DataMin,DataMax; bool IsApproximate,IsEmpty }`. `StreamEnvelope { uint StreamId; ChannelEnvelope[] Channels; bool IsApproximate }`. `XdfChannel.Envelope` (nullable).

- [ ] **Step 1 — failing test:** default `new ChannelEnvelope().IsEmpty` false-by-default, arrays null; a populated one exposes set values; `new XdfChannel().Envelope == null`. (Pure POCO — minimal.)
- [ ] **Step 2** RED. **Step 3 — implement** both POCOs (System-only) and add `public ChannelEnvelope Envelope { get; set; }` to `XdfChannel`. Keep `XdfChannel` WPF-free.
- [ ] **Step 4** GREEN + full suite. **Step 5** commit `Model/ChannelEnvelope.cs`, `Model/StreamEnvelope.cs`, `Model/XdfChannel.cs`, test.

---

### Task 15: Decimation math — `EnvelopeDecimator`

**Files:** Create `QuickLook.Plugin.XdfViewer/Xdf/EnvelopeDecimator.cs`; Test `…Tests/EnvelopeDecimatorTests.cs`.
**Consumes:** `ChannelEnvelope`. **Produces:** `internal sealed class EnvelopeDecimator` with ctor `(int channels, int buckets)`, `static int BucketOf(long globalIndex, long total, int buckets)`, `void Add(int channel, int bucket, double v)`, `ChannelEnvelope[] Build(bool approximate)`.

- [ ] **Step 1 — failing tests:** `BucketOf(0,N,W)==0`; `BucketOf(N-1,N,W)==W-1`; clamps above `W-1`; `total<=1`→0. Add known samples to 1 channel/4 buckets → assert per-bucket Min/Max. NaN/Inf ignored (bucket stays `Filled=false`). Flat channel → `DataMin==DataMax`. Channel with no adds → `IsEmpty`, and `DataMin/Max` sane. `Build(true)` sets `IsApproximate`.
- [ ] **Step 2** RED. **Step 3 — implement:**
```csharp
using System;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal sealed class EnvelopeDecimator
    {
        private readonly int _channels, _buckets;
        private readonly float[,] _min, _max;
        private readonly bool[,] _filled;

        public EnvelopeDecimator(int channels, int buckets)
        {
            _channels = channels; _buckets = Math.Max(1, buckets);
            _min = new float[_channels, _buckets];
            _max = new float[_channels, _buckets];
            _filled = new bool[_channels, _buckets];
        }

        public static int BucketOf(long globalIndex, long total, int buckets)
        {
            if (buckets <= 1 || total <= 1) return 0;
            long b = globalIndex * (long)buckets / total;
            if (b < 0) return 0;
            if (b >= buckets) return buckets - 1;
            return (int)b;
        }

        public void Add(int channel, int bucket, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return;
            if (channel < 0 || channel >= _channels || bucket < 0 || bucket >= _buckets) return;
            float f = (float)v;
            if (!_filled[channel, bucket]) { _min[channel, bucket] = f; _max[channel, bucket] = f; _filled[channel, bucket] = true; }
            else { if (f < _min[channel, bucket]) _min[channel, bucket] = f; if (f > _max[channel, bucket]) _max[channel, bucket] = f; }
        }

        public ChannelEnvelope[] Build(bool approximate)
        {
            var result = new ChannelEnvelope[_channels];
            for (int c = 0; c < _channels; c++)
            {
                var min = new float[_buckets]; var max = new float[_buckets]; var filled = new bool[_buckets];
                bool any = false; float dmin = float.MaxValue, dmax = float.MinValue;
                for (int b = 0; b < _buckets; b++)
                {
                    filled[b] = _filled[c, b];
                    if (filled[b]) { min[b] = _min[c, b]; max[b] = _max[c, b]; any = true;
                        if (min[b] < dmin) dmin = min[b]; if (max[b] > dmax) dmax = max[b]; }
                }
                result[c] = new ChannelEnvelope {
                    Min = min, Max = max, Filled = filled, BucketCount = _buckets,
                    DataMin = any ? dmin : 0f, DataMax = any ? dmax : 0f,
                    IsEmpty = !any, IsApproximate = approximate
                };
            }
            return result;
        }
    }
}
```
- [ ] **Step 4** GREEN + full suite. **Step 5** commit.

---

### Task 16: Reader — `FillEnvelopes` (index pass + strided decode)

**Files:** Modify `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs`; Create `Xdf/EnvelopeOptions.cs`; Test `…Tests/XdfReaderEnvelopeTests.cs`.
**Consumes:** `XdfFile`, `XdfSampleFormat`, `EnvelopeDecimator`, `StreamEnvelope`. **Produces:** `public static StreamEnvelope[] XdfReader.FillEnvelopes(XdfFile file, string path, EnvelopeOptions opts)` (+ `Stream` overload). Constants (next to `MaxMarkersPerStream`): `SparklineBuckets=100`, `ExpandedBuckets=500`, `MaxEnvelopeSamplesPerStream=20_000`, `MaxEnvelopeSamplesPerStreamHiRes=200_000`. `EnvelopeOptions { int Buckets; long MaxSamplesPerStream; uint? OnlyStreamId }`.

**Algorithm:**
- **Pass 1 (index):** verify magic; walk chunks exactly like `Read` but for `Samples` tag only, read `streamId`(4) + `numSamples`(varlen), record `ChunkRef{ streamId, long valuesStart = stream.Position, contentEnd, numSamples }`, then `Seek(contentEnd)`. Group `ChunkRef`s per stream in file order; compute each stream's `total = Σ numSamples` and each chunk's `cumBefore`.
- **Target streams:** those in `file.Streams` where `XdfSampleFormat.TryGetNumeric(ChannelFormat,…)` is true, `ChannelCount>0`, `total>0`, and (`opts.OnlyStreamId==null || id==OnlyStreamId`).
- **Chunk selection (cap):** `k = (total<=cap) ? 1 : (int)Math.Ceiling((double)total / cap)`; decode chunk `ci` when `ci % k == 0 || ci == last`. `approximate = k>1`.
- **Pass 2 (decode):** `new EnvelopeDecimator(channelCount, W)`; for each chosen chunk seek `valuesStart`, then `for (long i=0; i<numSamples && stream.Position<contentEnd; i++)`: read `tsMarker`(byte); if `==8` `ReadDouble()`; for each channel `c`: `v = read(reader)`; `g = cumBefore + i`; `decimator.Add(c, BucketOf(g,total,W), v)`. Build `StreamEnvelope{ StreamId, Channels = decimator.Build(approximate), IsApproximate=approximate }`.
- Wrap Pass 2 per-stream in try/catch (truncation-safe; skip that stream, continue). Never throw. **Do NOT touch `Read`.**

- [ ] **Step 1 — failing tests** (`XdfReaderEnvelopeTests`, using `SamplesNumeric`): (a) single chunk `int16` 1-ch samples `[0,10,-5,3]`, W=2 → bucket0 min0/max10, bucket1 min-5/max3. (b) two chunks (two `SamplesNumeric` calls) → global bucketing across both. (c) `MaxSamplesPerStream` small forces `IsApproximate` and `Filled` present in both the first and last bucket regions. (d) irregular stream (`nominal_srate=0`) still decodes. (e) zero-sample stream → not in result. (f) NaN payload (float32) skipped. (g) truncated tail → returns partial, no throw. (h) `OnlyStreamId` returns just that stream. (i) **regression:** `XdfReader.Read` on the same fixture is unchanged (spot-assert stream count/name).
- [ ] **Step 2** RED. **Step 3 — implement** per algorithm (reuse `ReadVarLength`; map format via `XdfSampleFormat`). **Step 4** GREEN + full suite (existing reader tests must stay green). **Step 5** commit `XdfReader.cs`, `EnvelopeOptions.cs`, test.

---

### Task 17: View models — `ChannelViewModel` + wiring

**Files:** Create `QuickLook.Plugin.XdfViewer/ViewModels/ChannelViewModel.cs`; Modify `ViewModels/StreamCardViewModel.cs`, `ViewModels/XdfOverviewViewModel.cs`; Test `…Tests/ChannelViewModelTests.cs`, extend `ViewModelTests.cs`.
**Produces:** `ChannelViewModel : INotifyPropertyChanged` — ctor `(XdfChannel channel, XdfStream stream)`; passthrough `string Label,Unit`; `ChannelEnvelope Sparkline`/`Detail` + `bool HasWaveform` (INPC); `bool IsExpanded` (INPC); `void RefreshEnvelope()` (reads `_channel.Envelope` → sets `Sparkline`, raises INPC); `void SetDetail(ChannelEnvelope)`; expose `StreamDurationSeconds` (from stream, for the expanded time axis). `StreamCardViewModel.Channels` → `IReadOnlyList<ChannelViewModel>`. `XdfOverviewViewModel.ApplyEnvelopes(StreamEnvelope[])` (loops streams→channels, assigns `XdfChannel.Envelope` then `RefreshEnvelope`).

- [ ] **Step 1 — failing tests:** `ChannelViewModel` raises `PropertyChanged("Sparkline")` and `HasWaveform` becomes true after setting the model channel's `Envelope` + `RefreshEnvelope()`; `Label`/`Unit` passthrough; `IsExpanded` raises INPC; `SetDetail` raises INPC. Extend `ViewModelTests`: `StreamCardViewModel.Channels[0].Label/Unit` still correct after the type change.
- [ ] **Step 2** RED. **Step 3 — implement** (INPC confined to `ChannelViewModel`; `StreamCardViewModel`/`XdfOverviewViewModel` stay otherwise immutable; build channel VMs in `StreamCardViewModel` ctor via `s.Channels.Select(c => new ChannelViewModel(c, s))`; `ExpanderHeader` keeps using the model channel count). **Step 4** GREEN + full suite. **Step 5** commit.

---

### Task 18: Render element — `WaveformSparkline`

**Files:** Create `QuickLook.Plugin.XdfViewer/Controls/WaveformSparkline.cs`; Test `…Tests/WaveformSparklineTests.cs`.
**Produces:** `WaveformSparkline : FrameworkElement` — DPs `ChannelEnvelope Envelope`, `Brush LineBrush`; `OnRender` draws one vertical line per filled bucket (min→max) via a cached `StreamGeometry`, autoscaled by `DataMin/DataMax`, flat channel → horizontal mid-line, unfilled buckets → gap. Static pure helpers `static double MapY(double v, double dataMin, double dataMax, double height)` and `static double MapX(int bucket, int bucketCount, double width)` (extracted for tests). Rebuild geometry only when `Envelope` changes.

- [ ] **Step 1 — failing tests (Map helpers only):** `MapX(0,W,width)==0`, `MapX(W-1,W,width)==width`, `bucketCount<=1`→0. `MapY(min,min,max,h)==h` (bottom), `MapY(max,min,max,h)==0` (top, inverted), `MapY(mid,…)≈h/2`, flat (`dataMax==dataMin`)→`h/2`, clamps outside `[min,max]` to `[0,h]`.
- [ ] **Step 2** RED. **Step 3 — implement** the control; keep `MapX`/`MapY` `internal static` pure. `OnRender` is NOT unit-tested. **Step 4** GREEN (Map tests) + build clean + full suite. **Step 5** commit.

---

### Task 19: Panel XAML/code-behind wiring  *(build + manual verify — no unit test)*

**Files:** Modify `QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml`, `XdfPreviewPanel.xaml.cs`.
- XAML: add controls xmlns; in the numeric-channel `DataTemplate`, make the row clickable and add `<controls:WaveformSparkline Envelope="{Binding Sparkline}" Width="100" Height="16"/>` beside the Label/Unit runs; below it an expanded panel (`Visibility` via `BoolToVis` on `IsExpanded`) with `<controls:WaveformSparkline Envelope="{Binding Detail}" Width="500" Height="120"/>` + amplitude (`DataMin`/`DataMax`) and `0 → {duration} s` labels. Enable virtualization on the channels `ItemsControl` (`VirtualizingStackPanel` items panel, `ScrollViewer.CanContentScroll=True`). Marker template unchanged.
- code-behind: `SetDataSource(string path, XdfFile file)`; `ApplyEnvelopes(StreamEnvelope[])` → `_vm.ApplyEnvelopes(...)`; row `MouseLeftButtonUp`/click handler → toggle the row's `ChannelViewModel.IsExpanded`; on first expand of its stream, `Task.Run(() => XdfReader.FillEnvelopes(file, path, hiResOpts{OnlyStreamId=stream, Buckets=ExpandedBuckets, MaxSamplesPerStream=HiRes}))` → `Dispatcher` → `SetDetail` on each channel VM of that stream (cache; guard file errors with try/catch, keep sparkline); propagate `LineBrush` (theme foreground) to sparklines in `ApplyTheme`.
- [ ] Build clean; confirm zero `PackageReference`. Commit. (Manual: developer verifies visuals later.)

---

### Task 20: Plugin two-phase load

**Files:** Modify `QuickLook.Plugin.XdfViewer/Plugin.cs`.
- In `View`, after `panel.ShowModel(vm)` and `context.IsBusy = false` (Phase 1 unchanged/instant), call `panel.SetDataSource(path, file)` and start a **second** `Task.Run` → `XdfReader.FillEnvelopes(file, path, new EnvelopeOptions{ Buckets=SparklineBuckets, MaxSamplesPerStream=MaxEnvelopeSamplesPerStream })` → `panel.Dispatcher.Invoke(() => vm.ApplyEnvelopes(envs))`. Wrap in try/catch (failure leaves the overview intact, no sparklines). Do not gate `IsBusy` on Phase 2.
- [ ] Build clean + full suite green (regression). Commit. (Manual: overview instant, sparklines pop in.)

---

### Task 21: Real-file opt-in + README

**Files:** Modify `QuickLook.Plugin.XdfViewer.Tests/RealFileSmokeTests.cs`, `README.md`.
- Add an opt-in test: when the sample `.xdf` is present, `XdfReader.Read` then `FillEnvelopes(sparkline opts)`; assert at least one numeric stream yields a `StreamEnvelope` whose channels have finite `DataMin/DataMax`, non-empty buckets, and decoded ≤ cap. Skips when absent.
- README: add a "Channel waveforms" subsection + manual-verification bullets (sparklines appear after overview; click-to-expand plot; dark/light legibility; marker streams show none).
- [ ] Full suite green. Commit.

---

## Self-review checklist
- Every spec item (sparkline, click-expand, bounded/approximate, no numeric decode in `Read`,
  zero PackageReference, WPF-free model/VM, chunk-level striding) maps to a task above. ✔
- No placeholders; pure-logic code complete; WPF/wiring tasks are build+manual by design. ✔
- Types consistent across tasks: `ChannelEnvelope`, `StreamEnvelope`, `EnvelopeOptions`,
  `FillEnvelopes`, `ChannelViewModel(Sparkline/Detail/IsExpanded/HasWaveform/RefreshEnvelope)`,
  `WaveformSparkline(Envelope/LineBrush/MapX/MapY)`. ✔
