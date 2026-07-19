param(
  [string]$Configuration = "Release",
  [string]$OutDir = "$PSScriptRoot\..\bin\$Configuration"
)
$ErrorActionPreference = "Stop"

$projDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$plugin  = "QuickLook.Plugin.XdfViewer.qlplugin"
$target  = Join-Path $projDir $plugin

# Build first so the packaged DLL always reflects current source. The previous script only zipped
# whatever already sat in bin\$Configuration, which silently shipped a stale DLL (packaged the
# previous day's Release build while all work had gone into Debug). dotnet failures don't throw,
# so check $LASTEXITCODE explicitly.
$csproj = Join-Path $projDir "QuickLook.Plugin.XdfViewer.csproj"
& dotnet build $csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "build failed ($Configuration); not packing a stale DLL" }

# QuickLook's installer reads "QuickLook.Plugin.Metadata.config" (NOT the ".Base"
# template) from the package, and requires /Metadata/Namespace + /Metadata/Version.
# Generate it from the template and stamp a version (git commit count; "0" fallback).
$baseConfig = Join-Path $projDir "QuickLook.Plugin.Metadata.Base.config"
$metaConfig = Join-Path $OutDir  "QuickLook.Plugin.Metadata.config"

[xml]$xml = Get-Content -LiteralPath $baseConfig
$rev = ""
try { $rev = (& git -C $projDir rev-list --count HEAD 2>$null) } catch { $rev = "" }
if ([string]::IsNullOrWhiteSpace($rev)) { $rev = "0" }
$xml.Metadata.Version = "$rev".Trim()
$xml.Save($metaConfig)

# Package = plugin DLL + the generated metadata config. (QuickLook.Common is provided
# by the host at runtime, so it is intentionally NOT bundled.)
if (Test-Path $target) { Remove-Item $target }
$include = @(
  (Join-Path $OutDir "QuickLook.Plugin.XdfViewer.dll"),
  $metaConfig
)
Compress-Archive -Path $include -DestinationPath $target
Write-Host "Packed $target (metadata Namespace=QuickLook.Plugin.XdfViewer, Version=$($xml.Metadata.Version))"
