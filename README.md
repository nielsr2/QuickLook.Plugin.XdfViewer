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

## Test

```
dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug
```
