param(
  [string]$Configuration = "Release",
  [string]$OutDir = "$PSScriptRoot\..\bin\$Configuration"
)
$ErrorActionPreference = "Stop"
$plugin = "QuickLook.Plugin.XdfViewer.qlplugin"
$target = Join-Path $PSScriptRoot "..\$plugin"
if (Test-Path $target) { Remove-Item $target }
# Ship only the plugin dll + its metadata config (QuickLook provides QuickLook.Common at runtime).
$include = @(
  (Join-Path $OutDir "QuickLook.Plugin.XdfViewer.dll"),
  (Join-Path $OutDir "QuickLook.Plugin.Metadata.Base.config")
)
Compress-Archive -Path $include -DestinationPath $target
Write-Host "Packed $target"
