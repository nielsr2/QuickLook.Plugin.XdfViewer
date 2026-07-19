# Project Status & Next Moves

> Working handoff doc (pre-publish). Delete before going fully public, or keep as a roadmap.
> Last updated: 2026-07-19.

## Where things stand

- Feature-complete QuickLook **XDF viewer** plugin. All recent work is on branch
  **`feat/channel-waveforms`** — **31 commits ahead of `master`, still unmerged**.
- **170 tests green**, build clean (0 warnings), plugin csproj keeps **zero `PackageReference`**,
  model/view-models stay WPF-free.
- Packaged as `QuickLook.Plugin.XdfViewer.qlplugin` via `Scripts/pack-zip.ps1` (which now **builds
  Release first**, so it can't ship a stale DLL again).

## Done this session

- **Full-window zoom view**: mouse-wheel zoom to *individual samples* (sample dots + a
  "sample-accurate" badge), four pan inputs (wheel/drag/minimap/scrollbar), top-right minimap,
  vertical padding, time + amplitude axes.
- **Windowed reads + cached chunk index** — only the visible window is decoded per zoom/pan; the
  file is indexed once. Stays responsive on ~200 MB recordings.
- **Device-aware wheel**: mouse → wheel pans, Shift+wheel zooms; trackpad → ↕ zooms, ↔ pans
  (via a `WM_MOUSEHWHEEL` window hook). Shift flips the default as an escape hatch.
- **Flat-channel warning** (grey ⚠ + tooltip) for constant/dead channels.
- **Time display**: durations roll up to `m`/`h` (`TimeFormat`); event/marker times shown
  **relative to the recording start** (not raw LSL clock); **footerless** files still get a duration.
- **Hardening**: channel-count sanity cap, per-value read-bounds guards.
- **Publish essentials**: GPL-3.0 `LICENSE`, GitHub Actions CI (`.github/workflows/build.yml`),
  README Download/License sections, untracked local dev notes.

## Approved but NOT yet built — resume here next session

1. **Full-view polish**: hover **crosshair** with time + amplitude readout; `Esc` to close;
   arrow/`+`/`-`/`Home`/`End` keyboard shortcuts; double-click to reset zoom-out.
2. **Marker overlay + channel search**: draw marker-stream **events as vertical lines** on the
   full-view time axis; a **filter/search box** to find channels in large (144-ch) streams.

## Needs you (can't be done from here)

- **Create the public GitHub repo + push** (no remote configured yet).
- **Cut a Release**: the CI auto-publishes the `.qlplugin` on a version tag —
  `git tag v1.0.0 && git push --tags`.
- **List it**: edit <https://github.com/QL-Win/QuickLook/wiki/Available-Plugins> (repo link, date,
  download link, description).
- **Add screenshots/GIF** to the README (placeholder note is already in place).

## Optional / nice-to-have

- `.xdfz` (gzip) support — real gap, medium effort (windowed reads need a seekable stream, so it
  needs a decompress-first path).
- Plugin **icon**; friendly `<Name>` in `QuickLook.Plugin.Metadata.Base.config`; reconcile
  `AssemblyInfo.cs` version (`0.1.0.0`) vs the git-commit-count package version.
- ClockOffset-corrected absolute/cross-stream timestamps (likely overkill for a preview).

## Before renaming the root dir + flattening the layout

Renaming **only the root dir** is safe — every internal path is relative. If you also **flatten**
(move the plugin project up / change folder layout), update these *together*, then re-test:

- **Git submodule** `QuickLook.Common` — `.gitmodules` path is
  `QuickLook.Plugin.XdfViewer/QuickLook.Common`. If the plugin folder moves, `git mv` it, fix
  `.gitmodules`, then `git submodule sync`.
- **`QuickLook.Plugin.XdfViewer.sln`** — references both project folders.
- **`QuickLook.Plugin.XdfViewer.Tests.csproj`** — `ProjectReference` to the plugin csproj (relative).
- **`Directory.Build.props`** (repo root) + root **`Scripts/update-version.ps1`** — QuickLook.Common's
  prebuild calls `$(SolutionDir)Scripts\update-version.ps1`; keep `Directory.Build.props` at the repo
  root, above all projects.
- **`QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1`** — resolves the project via `$PSScriptRoot\..`;
  keep it inside the plugin project's `Scripts/`.
- **`.github/workflows/build.yml`** — hardcodes `QuickLook.Plugin.XdfViewer/...` paths (csproj, pack
  script, artifact). Update to the new layout.
- Do it with this session **closed** (as planned) so nothing holds the directories open.

## Quick commands

```
# build / test / pack
dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Release
dotnet test  QuickLook.Plugin.XdfViewer.Tests -c Debug           # 170 tests
pwsh QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1 -Configuration Release

# merge the feature branch when you're happy
git checkout master && git merge --no-ff feat/channel-waveforms
```
