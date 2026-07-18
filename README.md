# QuickLook.Plugin.XdfViewer

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin that previews **XDF**
(Extensible Data Format — LabRecorder / Lab Streaming Layer) recordings: a
high-level overview of streams, channels, sample counts, durations, and markers —
without loading the raw sample data.

## Build

```
git clone --recursive <this repo>
dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Release
pwsh QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1 -Configuration Release
```

Produces `QuickLook.Plugin.XdfViewer.qlplugin`.

## Channel waveforms

Each numeric channel gets an inline min/max sparkline in its channel row, so
you can see roughly what the signal looks like without opening a full plotting
tool. Click a channel row to expand it into a larger, decimated waveform plot
with amplitude and time-range labels.

Waveform data is read with a bounded, approximate strided pass over the file —
independent of (and after) the instant structural overview, so the preview
window, stream/channel list, and summary bar always appear immediately and are
never blocked waiting on sample decoding. Very large streams are decimated
(strided sampling into a fixed bucket count) rather than fully decoded, so
memory and load time stay bounded regardless of recording size. Marker
streams are event streams, not waveforms, so their rows show timestamps/values
and no sparkline.

## Install

Open the `.qlplugin` with QuickLook (Space on the file), or drop it into
`%LocalAppData%\Programs\QuickLook\...\Plugins\` per the QuickLook docs, then
restart QuickLook.

## Manual verification checklist (developer runs this)

1. Press Space on a `.xdf` file in Explorer.
2. Expect: filename + summary bar (`N streams · <duration> · XDF v1.0`) and one
   card per stream.
3. Each card shows `name · type`, `channels · format`, `rate · samples`, duration.
4. Expand a data-stream card → channel labels/units (scrolls for 100+ channels).
5. Expand a marker-stream card → event timestamps + values.
6. A recording that crashed mid-capture shows an **incomplete** badge and still
   lists sample counts.
7. The stream/channel overview appears instantly; per-channel sparklines then
   fill in shortly after, without the overview ever appearing to stall.
8. Click a numeric channel row → a larger decimated waveform plot expands,
   labeled with amplitude and time range.
9. Sparklines and expanded plots are legible in both dark and light
   Windows/QuickLook themes.
10. A 144-channel EMG (e.g. Trigno) stream scrolls smoothly with sparklines
    rendered per row.

## Test

```
dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug
```
