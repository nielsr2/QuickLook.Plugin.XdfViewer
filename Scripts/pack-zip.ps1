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
# Generate it from the template and stamp $(PluginVersion) — the same single source
# of truth the DLL's AssemblyVersion is built from (see Directory.Build.props) — so
# the packaged metadata and the DLL version can never drift apart.
$baseConfig  = Join-Path $projDir "QuickLook.Plugin.Metadata.Base.config"
$metaConfig  = Join-Path $OutDir  "QuickLook.Plugin.Metadata.config"
$buildProps  = Join-Path $projDir "Directory.Build.props"

[xml]$propsXml = Get-Content -LiteralPath $buildProps
$version = ($propsXml.Project.PropertyGroup | Where-Object { $_.PluginVersion } | Select-Object -First 1).PluginVersion
if ([string]::IsNullOrWhiteSpace($version)) { throw "PluginVersion not found in $buildProps" }

[xml]$xml = Get-Content -LiteralPath $baseConfig
$xml.Metadata.Version = "$version".Trim()
$xml.Save($metaConfig)

# Package = plugin DLL + the generated metadata config. (QuickLook.Common is provided
# by the host at runtime, so it is intentionally NOT bundled.)
# Compress-Archive on PowerShell 7+ rejects a non-.zip -DestinationPath, so zip to a
# temp .zip first and rename — Windows PowerShell 5.1 (used locally) allows either,
# but pwsh 7 (GitHub's windows-latest runner) does not.
if (Test-Path $target) { Remove-Item $target }
$tempZip = Join-Path $projDir "QuickLook.Plugin.XdfViewer.zip"
if (Test-Path $tempZip) { Remove-Item $tempZip }
$include = @(
  (Join-Path $OutDir "QuickLook.Plugin.XdfViewer.dll"),
  $metaConfig
)
Compress-Archive -Path $include -DestinationPath $tempZip
Move-Item $tempZip $target
Write-Host "Packed $target (metadata Namespace=QuickLook.Plugin.XdfViewer, Version=$($xml.Metadata.Version))"
