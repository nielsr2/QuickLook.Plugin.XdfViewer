# QuickLook.Plugin.XdfViewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a QuickLook plugin that shows a fast, high-level overview (streams, channels, sample counts, durations, markers) of `.xdf` recordings without reading their sample payloads.

**Architecture:** A dependency-free, seek-based XDF chunk parser (`XdfReader`) turns a file into a plain `XdfFile` model. ViewModels format that model for display. A WPF `UserControl` (`XdfPreviewPanel`) renders the hybrid summary-bar + stream-cards UI. `Plugin.cs` implements QuickLook's `IViewer` and wires them together.

**Tech Stack:** C#, .NET Framework 4.6.2, WPF, `System.Xml.Linq` (in-framework), xUnit for tests. No external XDF library. `QuickLook.Common` as a git submodule + project reference.

## Global Constraints

Every task's requirements implicitly include this section. Values are copied verbatim from the spec.

- **Target framework:** `net462` (plugin). Test project `net462`.
- **Project/DLL/namespace:** `QuickLook.Plugin.XdfViewer` (DLL must match `QuickLook.Plugin.*`, namespace under `QuickLook.Plugin`).
- **No external XDF dependency** — the parser is hand-written. Only in-framework APIs (`System.IO`, `System.Xml.Linq`, `System.Collections.ObjectModel`).
- **Overview only** — never decode numeric sample values. The ONLY samples ever decoded are string/marker streams, for the event preview.
- **XDF byte rules:** all binary fields little-endian; strings UTF-8. Magic = 4 bytes `XDF:` (`0x58 0x44 0x46 0x3A`). Chunk = `[NumLengthBytes:1 ∈{1,4,8}][Length:NumLengthBytes LE][Tag:2 LE][Content]`; **`Length` counts `Tag`+`Content`, so content length = `Length − 2`; the next chunk starts at `(offset-of-Tag) + Length`.**
- **Chunk tags:** 1=FileHeader, 2=StreamHeader, 3=Samples, 4=ClockOffset, 5=Boundary, 6=StreamFooter.
- **Caps:** marker preview = first **500** events/stream; preferred window size = **750 × 550**; channels shown inline vs. collapsed threshold ≈ **32**.
- **No autonomous app/UI launch** (developer machine policy). QuickLook is never started by the implementer; UI is verified by unit-testable ViewModels + a manual checklist the developer runs themselves. All parser logic is covered by unit tests over committed synthetic fixtures.
- **Commits:** every task ends by committing. Never `git add -A`; add explicit paths. The local `*.xdf` file is gitignored and must never be committed.

---

## File Structure

```
QuickLook.Plugin.XdfViewer.sln
QuickLook.Plugin.XdfViewer/
├─ QuickLook.Plugin.XdfViewer.csproj
├─ Plugin.cs                          // IViewer
├─ XdfPreviewPanel.xaml / .xaml.cs    // WPF UserControl
├─ Properties/AssemblyInfo.cs
├─ QuickLook.Plugin.Metadata.Base.config
├─ Xdf/
│  ├─ XdfChunkTag.cs
│  ├─ BinaryReaderExtensions.cs
│  ├─ XdfReader.cs
│  └─ Model/ XdfFile.cs, XdfStream.cs, XdfChannel.cs, XdfMarker.cs
├─ ViewModels/ XdfOverviewViewModel.cs, StreamCardViewModel.cs
├─ Resources/ (icon + theme brushes)
├─ Scripts/ pack-zip.ps1, update-version.ps1
└─ QuickLook.Common/                   // git submodule
QuickLook.Plugin.XdfViewer.Tests/
├─ QuickLook.Plugin.XdfViewer.Tests.csproj
├─ XdfFixtureBuilder.cs
└─ *Tests.cs
```

---

### Task 1: Solution + project scaffold + QuickLook.Common submodule

**Files:**
- Create: `QuickLook.Plugin.XdfViewer.sln`
- Create: `QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj`
- Create: `QuickLook.Plugin.XdfViewer/Properties/AssemblyInfo.cs`
- Create: `QuickLook.Plugin.XdfViewer/QuickLook.Plugin.Metadata.Base.config`
- Create: `QuickLook.Plugin.XdfViewer/Scripts/update-version.ps1`
- Create: `QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1`
- Submodule: `QuickLook.Plugin.XdfViewer/QuickLook.Common`

**Interfaces:**
- Produces: a buildable (empty) plugin assembly `QuickLook.Plugin.XdfViewer.dll` and a `QuickLook.Common` project reference for later tasks.

- [ ] **Step 1: Add the QuickLook.Common submodule**

Run:
```bash
git submodule add https://github.com/QL-Win/QuickLook.Common.git QuickLook.Plugin.XdfViewer/QuickLook.Common
git submodule update --init --recursive
```
Expected: `QuickLook.Plugin.XdfViewer/QuickLook.Common/QuickLook.Common.csproj` exists.

- [ ] **Step 2: Create the plugin csproj**

Create `QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>false</UseWindowsForms>
    <LangVersion>latest</LangVersion>
    <Platforms>AnyCPU;x64</Platforms>
    <RootNamespace>QuickLook.Plugin.XdfViewer</RootNamespace>
    <AssemblyName>QuickLook.Plugin.XdfViewer</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <Deterministic>true</Deterministic>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="QuickLook.Common\QuickLook.Common.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

  <!-- Do not compile the submodule's own sources into this assembly -->
  <ItemGroup>
    <Compile Remove="QuickLook.Common\**" />
    <Page Remove="QuickLook.Common\**" />
    <EmbeddedResource Remove="QuickLook.Common\**" />
    <None Remove="QuickLook.Common\**" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create AssemblyInfo, metadata config, and Scripts**

Create `QuickLook.Plugin.XdfViewer/Properties/AssemblyInfo.cs`:
```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("QuickLook.Plugin.XdfViewer")]
[assembly: AssemblyProduct("QuickLook.Plugin.XdfViewer")]
[assembly: AssemblyDescription("QuickLook plugin: overview of XDF (LabRecorder/LSL) recordings")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
```

Create `QuickLook.Plugin.XdfViewer/QuickLook.Plugin.Metadata.Base.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Metadata>
  <Name>XDF Viewer</Name>
  <Version>0.1.0</Version>
  <QuickLookVersion>3.6.0</QuickLookVersion>
</Metadata>
```

Create `QuickLook.Plugin.XdfViewer/Scripts/update-version.ps1`:
```powershell
# Placeholder for CI version stamping; no-op locally so pre-build never fails.
Write-Host "update-version: no-op (local build)"
exit 0
```

Create `QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1`:
```powershell
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
```

- [ ] **Step 4: Create the solution and add both projects**

Run:
```bash
dotnet new sln -n QuickLook.Plugin.XdfViewer
dotnet sln add QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj
dotnet sln add QuickLook.Plugin.XdfViewer/QuickLook.Common/QuickLook.Common.csproj
```
Expected: `QuickLook.Plugin.XdfViewer.sln` lists both projects.

- [ ] **Step 5: Build to verify the scaffold**

Run: `dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Debug`
Expected: `Build succeeded`. (If the WPF/net462 build needs full MSBuild, run `msbuild QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj /t:Build` from a Developer prompt instead — note which worked in the commit message.)

- [ ] **Step 6: Commit**

```bash
git add QuickLook.Plugin.XdfViewer.sln QuickLook.Plugin.XdfViewer/ .gitmodules
git commit -m "chore: scaffold XdfViewer plugin project + QuickLook.Common submodule"
```

---

### Task 2: Binary read helpers + chunk-tag enum

**Files:**
- Create: `QuickLook.Plugin.XdfViewer/Xdf/XdfChunkTag.cs`
- Create: `QuickLook.Plugin.XdfViewer/Xdf/BinaryReaderExtensions.cs`
- Create: `QuickLook.Plugin.XdfViewer.Tests/QuickLook.Plugin.XdfViewer.Tests.csproj`
- Test: `QuickLook.Plugin.XdfViewer.Tests/BinaryReaderExtensionsTests.cs`

**Interfaces:**
- Produces:
  - `enum XdfChunkTag { FileHeader=1, StreamHeader=2, Samples=3, ClockOffset=4, Boundary=5, StreamFooter=6 }` in namespace `QuickLook.Plugin.XdfViewer.Xdf`.
  - `static long BinaryReaderExtensions.ReadVarLength(this BinaryReader r)` — reads one `NumLengthBytes` byte (1/4/8), then that many little-endian bytes, returns the value as `long`. Throws `InvalidDataException` on any other count.

- [ ] **Step 1: Create the test project**

Create `QuickLook.Plugin.XdfViewer.Tests/QuickLook.Plugin.XdfViewer.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\QuickLook.Plugin.XdfViewer\QuickLook.Plugin.XdfViewer.csproj" />
  </ItemGroup>
</Project>
```
Then run: `dotnet sln add QuickLook.Plugin.XdfViewer.Tests/QuickLook.Plugin.XdfViewer.Tests.csproj`

- [ ] **Step 2: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/BinaryReaderExtensionsTests.cs`:
```csharp
using System.IO;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class BinaryReaderExtensionsTests
    {
        private static long ReadVar(params byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var r = new BinaryReader(ms))
                return r.ReadVarLength();
        }

        [Fact] public void OneByteLength() => Assert.Equal(0x77, ReadVar(0x01, 0x77));

        [Fact] public void FourByteLength_LittleEndian()
            => Assert.Equal(0x3c14, ReadVar(0x04, 0x14, 0x3c, 0x00, 0x00));

        [Fact] public void EightByteLength_LittleEndian()
            => Assert.Equal(1L, ReadVar(0x08, 0x01, 0, 0, 0, 0, 0, 0, 0));

        [Fact] public void InvalidCountThrows()
            => Assert.Throws<InvalidDataException>(() => ReadVar(0x03, 0, 0, 0));
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug`
Expected: FAIL — `BinaryReaderExtensions`/`ReadVarLength` does not exist (compile error).

- [ ] **Step 4: Implement the helpers**

Create `QuickLook.Plugin.XdfViewer/Xdf/XdfChunkTag.cs`:
```csharp
namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal enum XdfChunkTag
    {
        FileHeader = 1,
        StreamHeader = 2,
        Samples = 3,
        ClockOffset = 4,
        Boundary = 5,
        StreamFooter = 6
    }
}
```

Create `QuickLook.Plugin.XdfViewer/Xdf/BinaryReaderExtensions.cs`:
```csharp
using System.IO;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal static class BinaryReaderExtensions
    {
        /// <summary>
        /// Reads an XDF variable-length integer: one NumLengthBytes byte (1, 4 or 8)
        /// followed by that many little-endian bytes. BinaryReader is always little-endian.
        /// </summary>
        public static long ReadVarLength(this BinaryReader r)
        {
            byte n = r.ReadByte();
            switch (n)
            {
                case 1: return r.ReadByte();
                case 4: return r.ReadUInt32();
                case 8: return (long)r.ReadUInt64();
                default:
                    throw new InvalidDataException($"Invalid XDF NumLengthBytes: {n}");
            }
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/XdfChunkTag.cs QuickLook.Plugin.XdfViewer/Xdf/BinaryReaderExtensions.cs QuickLook.Plugin.XdfViewer.Tests/ QuickLook.Plugin.XdfViewer.sln
git commit -m "feat: XDF binary read helpers + chunk-tag enum"
```

---

### Task 3: Data model

**Files:**
- Create: `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfChannel.cs`
- Create: `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfMarker.cs`
- Create: `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfStream.cs`
- Create: `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfFile.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfStreamTests.cs`

**Interfaces:**
- Produces (namespace `QuickLook.Plugin.XdfViewer.Xdf.Model`):
  - `XdfChannel { string Label; string Unit; string Type }`
  - `XdfMarker { double Timestamp; string Value }`
  - `XdfStream { uint Id; string Name; string Type; int ChannelCount; double NominalSampleRate; string ChannelFormat; double? FirstTimestamp; double? LastTimestamp; long? SampleCount; double? MeasuredSampleRate; long TalliedSampleCount; bool IsIncomplete; bool MarkersTruncated; List<XdfChannel> Channels; List<XdfMarker> Markers; bool IsMarkerStream {get;} double? Duration {get;} }`
  - `XdfFile { string Version; System.DateTimeOffset? RecordedAt; bool IsTruncated; List<XdfStream> Streams; double? TotalDuration {get;} }`

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfStreamTests.cs`:
```csharp
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfStreamTests
    {
        [Fact]
        public void Duration_FromTimestamps_WhenPresent()
        {
            var s = new XdfStream { FirstTimestamp = 10.0, LastTimestamp = 73.5 };
            Assert.Equal(63.5, s.Duration.Value, 3);
        }

        [Fact]
        public void Duration_FromSampleCountAndRate_WhenNoTimestamps()
        {
            var s = new XdfStream { NominalSampleRate = 512, SampleCount = 1024 };
            Assert.Equal(2.0, s.Duration.Value, 3);
        }

        [Fact]
        public void Duration_Null_WhenIrregularAndNoTimestamps()
        {
            var s = new XdfStream { NominalSampleRate = 0, SampleCount = 18 };
            Assert.Null(s.Duration);
        }

        [Fact]
        public void IsMarkerStream_WhenStringFormat()
        {
            Assert.True(new XdfStream { ChannelFormat = "string" }.IsMarkerStream);
            Assert.False(new XdfStream { ChannelFormat = "float32" }.IsMarkerStream);
        }

        [Fact]
        public void TotalDuration_SpansMinFirstToMaxLast()
        {
            var f = new XdfFile();
            f.Streams.Add(new XdfStream { FirstTimestamp = 1.0, LastTimestamp = 5.0 });
            f.Streams.Add(new XdfStream { FirstTimestamp = 2.0, LastTimestamp = 9.0 });
            Assert.Equal(8.0, f.TotalDuration.Value, 3);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfStreamTests -c Debug`
Expected: FAIL — model types do not exist.

- [ ] **Step 3: Implement the model**

Create `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfChannel.cs`:
```csharp
namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfChannel
    {
        public string Label { get; set; }
        public string Unit { get; set; }
        public string Type { get; set; }
    }
}
```

Create `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfMarker.cs`:
```csharp
namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfMarker
    {
        public double Timestamp { get; set; }
        public string Value { get; set; }
    }
}
```

Create `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfStream.cs`:
```csharp
using System.Collections.Generic;

namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfStream
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int ChannelCount { get; set; }
        public double NominalSampleRate { get; set; }
        public string ChannelFormat { get; set; }

        public double? FirstTimestamp { get; set; }
        public double? LastTimestamp { get; set; }
        public long? SampleCount { get; set; }
        public double? MeasuredSampleRate { get; set; }

        /// <summary>Sample count summed from Samples chunks; fallback when no footer.</summary>
        public long TalliedSampleCount { get; set; }

        public bool IsIncomplete { get; set; }
        public bool MarkersTruncated { get; set; }

        public List<XdfChannel> Channels { get; } = new List<XdfChannel>();
        public List<XdfMarker> Markers { get; } = new List<XdfMarker>();

        public bool IsMarkerStream =>
            string.Equals(ChannelFormat, "string", System.StringComparison.OrdinalIgnoreCase);

        public double? Duration
        {
            get
            {
                if (FirstTimestamp.HasValue && LastTimestamp.HasValue)
                    return LastTimestamp.Value - FirstTimestamp.Value;
                if (NominalSampleRate > 0 && SampleCount.HasValue)
                    return SampleCount.Value / NominalSampleRate;
                return null;
            }
        }
    }
}
```

Create `QuickLook.Plugin.XdfViewer/Xdf/Model/XdfFile.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class XdfFile
    {
        public string Version { get; set; }
        public DateTimeOffset? RecordedAt { get; set; }
        public bool IsTruncated { get; set; }
        public List<XdfStream> Streams { get; } = new List<XdfStream>();

        /// <summary>Wall-clock span from the earliest first_timestamp to the latest last_timestamp.</summary>
        public double? TotalDuration
        {
            get
            {
                var firsts = Streams.Where(s => s.FirstTimestamp.HasValue)
                                    .Select(s => s.FirstTimestamp.Value).ToList();
                var lasts = Streams.Where(s => s.LastTimestamp.HasValue)
                                   .Select(s => s.LastTimestamp.Value).ToList();
                if (firsts.Count > 0 && lasts.Count > 0)
                    return lasts.Max() - firsts.Min();
                return Streams.Select(s => s.Duration).Where(d => d.HasValue)
                              .Select(d => d.Value).DefaultIfEmpty().Max() is double m && m > 0 ? m : (double?)null;
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfStreamTests -c Debug`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/Model/ QuickLook.Plugin.XdfViewer.Tests/XdfStreamTests.cs
git commit -m "feat: XDF data model (file/stream/channel/marker) with derived durations"
```

---

### Task 4: Test fixture builder

**Files:**
- Create: `QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilder.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilderTests.cs`

**Interfaces:**
- Produces `XdfFixtureBuilder` (test helper) that writes spec-correct XDF bytes:
  - `XdfFixtureBuilder FileHeader(string version = "1.0", string datetime = null)`
  - `XdfFixtureBuilder StreamHeaderXml(uint id, string xml)`
  - `XdfFixtureBuilder StreamHeader(uint id, string name, string type, int channelCount, double srate, string format, params (string label, string unit)[] channels)`
  - `XdfFixtureBuilder SamplesRaw(uint id, long numSamples, int fillerBytes = 0)`
  - `XdfFixtureBuilder SamplesString(uint id, params (double ts, string val)[] markers)`
  - `XdfFixtureBuilder StreamFooter(uint id, double first, double last, long count)`
  - `XdfFixtureBuilder Raw(byte[] bytes)` (append arbitrary bytes, for truncation tests)
  - `byte[] ToBytes()` / `MemoryStream ToStream()`
  - Static `byte[] WithoutMagic(...)` not needed; add `bool omitMagic` via ctor `XdfFixtureBuilder(bool withMagic = true)`.

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilderTests.cs`:
```csharp
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfFixtureBuilderTests
    {
        [Fact]
        public void StartsWithMagic()
        {
            var bytes = new XdfFixtureBuilder().ToBytes();
            Assert.Equal(new byte[] { 0x58, 0x44, 0x46, 0x3A }, new[] { bytes[0], bytes[1], bytes[2], bytes[3] });
        }

        [Fact]
        public void FileHeaderChunk_HasCorrectLengthAndTag()
        {
            // FileHeader with a tiny known XML content.
            var bytes = new XdfFixtureBuilder().StreamHeaderXml(0, "<info/>").ToBytes();
            // after 4-byte magic: NumLengthBytes(=4), Length(4 LE), Tag(2 LE)=2, StreamID(4), content
            Assert.Equal(0x04, bytes[4]);                       // NumLengthBytes
            int len = bytes[5] | (bytes[6] << 8) | (bytes[7] << 16) | (bytes[8] << 24);
            Assert.Equal(0x02, bytes[9]);                       // Tag low byte = StreamHeader
            Assert.Equal(0x00, bytes[10]);                      // Tag high byte
            // content length = len - 2 (tag); next chunk would start at offset 9 + len
            Assert.True(len >= 2 + 4);                          // tag + streamId at minimum
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfFixtureBuilderTests -c Debug`
Expected: FAIL — `XdfFixtureBuilder` does not exist.

- [ ] **Step 3: Implement the fixture builder**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilder.cs`:
```csharp
using System.Globalization;
using System.IO;
using System.Text;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>Writes spec-correct XDF bytes for tests. Uses 4-byte chunk lengths.</summary>
    public sealed class XdfFixtureBuilder
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly BinaryWriter _bw;

        public XdfFixtureBuilder(bool withMagic = true)
        {
            _bw = new BinaryWriter(_ms, Encoding.UTF8, leaveOpen: true);
            if (withMagic) _bw.Write(new byte[] { 0x58, 0x44, 0x46, 0x3A }); // "XDF:"
        }

        private void Chunk(ushort tag, byte[] content)
        {
            _bw.Write((byte)4);                          // NumLengthBytes
            _bw.Write((uint)(content.Length + 2));       // Length = tag(2) + content
            _bw.Write(tag);                              // Tag (LE)
            _bw.Write(content);
        }

        private static void WriteVarLen(BinaryWriter w, long value)
        {
            w.Write((byte)4);
            w.Write((uint)value);
        }

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

        public XdfFixtureBuilder FileHeader(string version = "1.0", string datetime = null)
        {
            var dt = datetime == null ? "" : $"<datetime>{datetime}</datetime>";
            Chunk(1, Utf8($"<?xml version=\"1.0\"?><info><version>{version}</version>{dt}</info>"));
            return this;
        }

        public XdfFixtureBuilder StreamHeaderXml(uint id, string xml)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                w.Write(Utf8(xml));
                Chunk(2, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder StreamHeader(uint id, string name, string type,
            int channelCount, double srate, string format, params (string label, string unit)[] channels)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?><info>");
            sb.Append($"<name>{name}</name><type>{type}</type>");
            sb.Append($"<channel_count>{channelCount}</channel_count>");
            sb.Append($"<nominal_srate>{srate.ToString(CultureInfo.InvariantCulture)}</nominal_srate>");
            sb.Append($"<channel_format>{format}</channel_format>");
            if (channels != null && channels.Length > 0)
            {
                sb.Append("<desc><channels>");
                foreach (var c in channels)
                    sb.Append($"<channel><label>{c.label}</label><unit>{c.unit}</unit></channel>");
                sb.Append("</channels></desc>");
            }
            sb.Append("</info>");
            return StreamHeaderXml(id, sb.ToString());
        }

        public XdfFixtureBuilder SamplesRaw(uint id, long numSamples, int fillerBytes = 0)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, numSamples);
                if (fillerBytes > 0) w.Write(new byte[fillerBytes]);
                Chunk(3, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder SamplesString(uint id, params (double ts, string val)[] markers)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, markers.Length);
                foreach (var mk in markers)
                {
                    w.Write((byte)8);           // timestamp present
                    w.Write(mk.ts);             // double LE
                    var val = Utf8(mk.val);
                    WriteVarLen(w, val.Length);  // string value length
                    w.Write(val);
                }
                Chunk(3, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder StreamFooter(uint id, double first, double last, long count)
        {
            var ci = CultureInfo.InvariantCulture;
            var xml = "<?xml version=\"1.0\"?><info>" +
                      $"<first_timestamp>{first.ToString(ci)}</first_timestamp>" +
                      $"<last_timestamp>{last.ToString(ci)}</last_timestamp>" +
                      $"<sample_count>{count}</sample_count></info>";
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                w.Write(Utf8(xml));
                Chunk(6, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder Raw(byte[] bytes) { _bw.Write(bytes); return this; }

        public byte[] ToBytes() { _bw.Flush(); return _ms.ToArray(); }
        public MemoryStream ToStream() => new MemoryStream(ToBytes());
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfFixtureBuilderTests -c Debug`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilder.cs QuickLook.Plugin.XdfViewer.Tests/XdfFixtureBuilderTests.cs
git commit -m "test: XDF fixture builder for synthetic .xdf bytes"
```

---

### Task 5: Reader — magic, FileHeader, StreamHeader

**Files:**
- Create: `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfReaderHeaderTests.cs`

**Interfaces:**
- Consumes: `BinaryReaderExtensions.ReadVarLength`, `XdfChunkTag`, model types, `XdfFixtureBuilder`.
- Produces:
  - `public static XdfFile XdfReader.Read(Stream stream)` and `public static XdfFile XdfReader.Read(string path)` in namespace `QuickLook.Plugin.XdfViewer.Xdf`. Returns `null` when the magic is absent. This task parses FileHeader (`Version`, `RecordedAt`) and StreamHeader (id, name, type, channel_count, nominal_srate, channel_format, channels); footers/samples added in later tasks (their chunks are skipped for now via the resync seek).
  - `public const int XdfReader.MaxMarkersPerStream = 500;`

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfReaderHeaderTests.cs`:
```csharp
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderHeaderTests
    {
        [Fact]
        public void ParsesFileHeaderAndStreamHeader()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader("1.0", "2026-04-16T16:48:17+0200")
                .StreamHeader(2, "Trigno-AUX", "IMU", 144, 148.148, "float32",
                    ("ACCX1", "g"), ("ACCY1", "g"))
                .ToStream();

            var file = XdfReader.Read(bytes);

            Assert.Equal("1.0", file.Version);
            var s = Assert.Single(file.Streams);
            Assert.Equal(2u, s.Id);
            Assert.Equal("Trigno-AUX", s.Name);
            Assert.Equal("IMU", s.Type);
            Assert.Equal(144, s.ChannelCount);
            Assert.Equal("float32", s.ChannelFormat);
            Assert.Equal(148.148, s.NominalSampleRate, 3);
            Assert.Equal(2, s.Channels.Count);
            Assert.Equal("ACCX1", s.Channels[0].Label);
            Assert.Equal("g", s.Channels[0].Unit);
        }

        [Fact]
        public void ReturnsNull_WhenNotXdf()
        {
            var bytes = new XdfFixtureBuilder(withMagic: false).Raw(new byte[] { 1, 2, 3, 4 }).ToStream();
            Assert.Null(XdfReader.Read(bytes));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderHeaderTests -c Debug`
Expected: FAIL — `XdfReader` does not exist.

- [ ] **Step 3: Implement the reader (headers only; skip other chunks)**

Create `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    public static class XdfReader
    {
        public const int MaxMarkersPerStream = 500;
        private static readonly byte[] Magic = { 0x58, 0x44, 0x46, 0x3A }; // "XDF:"

        public static XdfFile Read(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                           1 << 16, FileOptions.SequentialScan))
                return Read(fs);
        }

        public static XdfFile Read(Stream stream)
        {
            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (magic.Length < 4 || !magic.SequenceEqual(Magic))
                return null;

            var file = new XdfFile();
            var streams = new Dictionary<uint, XdfStream>();
            var order = new List<uint>();

            try
            {
                while (stream.Position < stream.Length)
                {
                    long len = reader.ReadVarLength();
                    long tagPos = stream.Position;
                    long contentEnd = tagPos + len;
                    if (len < 2 || contentEnd > stream.Length)
                    {
                        file.IsTruncated = true;
                        break;
                    }
                    var tag = (XdfChunkTag)reader.ReadUInt16();

                    switch (tag)
                    {
                        case XdfChunkTag.FileHeader:
                            ParseFileHeader(ReadXml(reader, contentEnd), file);
                            break;
                        case XdfChunkTag.StreamHeader:
                            ParseStreamHeader(reader, contentEnd, streams, order);
                            break;
                        // Samples/StreamFooter/ClockOffset/Boundary: handled in later tasks or skipped.
                    }

                    stream.Seek(contentEnd, SeekOrigin.Begin); // always resync to chunk boundary
                }
            }
            catch (EndOfStreamException) { file.IsTruncated = true; }
            catch (Exception) { file.IsTruncated = true; }

            foreach (var id in order)
                file.Streams.Add(streams[id]);
            return file;
        }

        private static string ReadXml(BinaryReader reader, long contentEnd)
        {
            int len = (int)(contentEnd - reader.BaseStream.Position);
            if (len <= 0) return string.Empty;
            var bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void ParseFileHeader(string xml, XdfFile file)
        {
            var info = SafeRootInfo(xml);
            if (info == null) return;
            file.Version = (string)info.Element("version");
            var dt = (string)info.Element("datetime");
            if (DateTimeOffset.TryParse(dt, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
                file.RecordedAt = parsed;
        }

        private static void ParseStreamHeader(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            var info = SafeRootInfo(ReadXml(reader, contentEnd));
            var s = GetOrAdd(streams, order, id);
            if (info == null) return;

            s.Name = (string)info.Element("name");
            s.Type = (string)info.Element("type");
            s.ChannelFormat = (string)info.Element("channel_format");
            s.ChannelCount = ParseInt(info.Element("channel_count"), 0);
            s.NominalSampleRate = ParseDouble(info.Element("nominal_srate"), 0);

            var channels = info.Element("desc")?.Element("channels");
            if (channels != null)
            {
                foreach (var ch in channels.Elements("channel"))
                    s.Channels.Add(new XdfChannel
                    {
                        Label = (string)ch.Element("label"),
                        Unit = (string)ch.Element("unit"),
                        Type = (string)ch.Element("type")
                    });
            }
        }

        private static XdfStream GetOrAdd(Dictionary<uint, XdfStream> streams, List<uint> order, uint id)
        {
            if (!streams.TryGetValue(id, out var s))
            {
                s = new XdfStream { Id = id };
                streams[id] = s;
                order.Add(id);
            }
            return s;
        }

        private static XElement SafeRootInfo(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try { return XDocument.Parse(xml).Root; }
            catch { return null; }
        }

        private static int ParseInt(XElement e, int fallback)
            => e != null && int.TryParse(e.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static double ParseDouble(XElement e, double fallback)
            => e != null && double.TryParse(e.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderHeaderTests -c Debug`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs QuickLook.Plugin.XdfViewer.Tests/XdfReaderHeaderTests.cs
git commit -m "feat: XDF reader parses magic, FileHeader and StreamHeader"
```

---

### Task 6: Reader — StreamFooter + merge

**Files:**
- Modify: `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs` (add footer case + parser)
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfReaderFooterTests.cs`

**Interfaces:**
- Consumes: everything from Task 5.
- Produces: after `Read`, a stream that had a `StreamFooter` has `FirstTimestamp`, `LastTimestamp`, `SampleCount`, `MeasuredSampleRate` populated and `Duration == Last − First`.

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfReaderFooterTests.cs`:
```csharp
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderFooterTests
    {
        [Fact]
        public void MergesFooterCountAndDuration()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 100, fillerBytes: 8)
                .StreamFooter(1, first: 0.0, last: 63.5, count: 32512)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.Equal(32512, s.SampleCount.Value);
            Assert.Equal(0.0, s.FirstTimestamp.Value, 3);
            Assert.Equal(63.5, s.LastTimestamp.Value, 3);
            Assert.Equal(63.5, s.Duration.Value, 3);
            Assert.False(s.IsIncomplete);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderFooterTests -c Debug`
Expected: FAIL — `SampleCount`/`FirstTimestamp` are null (footer not parsed yet).

- [ ] **Step 3: Add the footer case + parser**

In `XdfReader.cs`, inside the `switch (tag)`, add after the `StreamHeader` case:
```csharp
                        case XdfChunkTag.StreamFooter:
                            ParseStreamFooter(reader, contentEnd, streams, order);
                            break;
```

Add this method to `XdfReader`:
```csharp
        private static void ParseStreamFooter(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            var info = SafeRootInfo(ReadXml(reader, contentEnd));
            var s = GetOrAdd(streams, order, id);
            if (info == null) return;

            var first = info.Element("first_timestamp");
            var last = info.Element("last_timestamp");
            var count = info.Element("sample_count");
            var srate = info.Element("measured_srate");

            if (first != null) s.FirstTimestamp = ParseDouble(first, 0);
            if (last != null) s.LastTimestamp = ParseDouble(last, 0);
            if (count != null) s.SampleCount = (long)ParseDouble(count, 0);
            if (srate != null) s.MeasuredSampleRate = ParseDouble(srate, 0);
        }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderFooterTests -c Debug`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs QuickLook.Plugin.XdfViewer.Tests/XdfReaderFooterTests.cs
git commit -m "feat: XDF reader parses StreamFooter (counts, timestamps, duration)"
```

---

### Task 7: Reader — Samples tally + incomplete flag

**Files:**
- Modify: `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs` (add Samples case + tally + finalize)
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfReaderIncompleteTests.cs`

**Interfaces:**
- Consumes: Tasks 5–6.
- Produces: when a stream has NO footer, `Read` sets `SampleCount = TalliedSampleCount` (sum of Samples-chunk sample counts) and `IsIncomplete = true`. When a footer IS present, its `SampleCount` wins and `IsIncomplete` stays false.

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfReaderIncompleteTests.cs`:
```csharp
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderIncompleteTests
    {
        [Fact]
        public void NoFooter_TalliesSamplesAndFlagsIncomplete()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 300, fillerBytes: 4)
                .SamplesRaw(1, 200, fillerBytes: 4)
                .ToStream(); // no footer

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.True(s.IsIncomplete);
            Assert.Equal(500, s.SampleCount.Value);
        }

        [Fact]
        public void FooterPresent_FooterCountWins_NotIncomplete()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                .SamplesRaw(1, 300, fillerBytes: 4)
                .StreamFooter(1, 0, 1, count: 300)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.False(s.IsIncomplete);
            Assert.Equal(300, s.SampleCount.Value);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderIncompleteTests -c Debug`
Expected: FAIL — `IsIncomplete` false / `SampleCount` null (Samples not tallied).

- [ ] **Step 3: Add the Samples case, tally, and finalize step**

In `XdfReader.cs` `switch (tag)`, add:
```csharp
                        case XdfChunkTag.Samples:
                            ParseSamples(reader, contentEnd, streams, order);
                            break;
```

Add this method (marker decoding comes in Task 8; here we only tally):
```csharp
        private static void ParseSamples(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            long numSamples = reader.ReadVarLength();
            var s = GetOrAdd(streams, order, id);
            s.TalliedSampleCount += numSamples;
            // Value bytes are skipped by the caller's resync seek (Task 8 decodes marker streams).
        }
```

Replace the finalize loop at the end of `Read` (the `foreach (var id in order) file.Streams.Add(...)`) with:
```csharp
            foreach (var id in order)
            {
                var s = streams[id];
                if (!s.SampleCount.HasValue)
                {
                    if (s.TalliedSampleCount > 0) s.SampleCount = s.TalliedSampleCount;
                    s.IsIncomplete = true; // no footer was seen
                }
                file.Streams.Add(s);
            }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter XdfReaderIncompleteTests -c Debug`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs QuickLook.Plugin.XdfViewer.Tests/XdfReaderIncompleteTests.cs
git commit -m "feat: XDF reader tallies Samples and flags footerless streams incomplete"
```

---

### Task 8: Reader — marker decode, cap, and truncation robustness

**Files:**
- Modify: `QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs` (decode string samples; guard truncation)
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfReaderMarkerTests.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/XdfReaderRobustnessTests.cs`

**Interfaces:**
- Consumes: Tasks 5–7.
- Produces: for `ChannelFormat == "string"` streams, `Markers` holds up to `MaxMarkersPerStream` `XdfMarker{Timestamp,Value}` entries decoded from Samples chunks; `MarkersTruncated` is set when more existed. A truncated/corrupt file returns the streams parsed so far with `IsTruncated == true` and never throws.

- [ ] **Step 1: Write the failing tests**

Create `QuickLook.Plugin.XdfViewer.Tests/XdfReaderMarkerTests.cs`:
```csharp
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderMarkerTests
    {
        [Fact]
        public void DecodesMarkerValuesAndTimestamps()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(7, "Markers", "Markers", 1, 0, "string")
                .SamplesString(7, (12.3, "stim_on"), (14.9, "stim_off"))
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.True(s.IsMarkerStream);
            Assert.Equal(2, s.Markers.Count);
            Assert.Equal(12.3, s.Markers[0].Timestamp, 3);
            Assert.Equal("stim_on", s.Markers[0].Value);
            Assert.Equal("stim_off", s.Markers[1].Value);
        }

        [Fact]
        public void CapsMarkersAndSetsTruncatedFlag()
        {
            var markers = Enumerable.Range(0, XdfReader.MaxMarkersPerStream + 25)
                .Select(i => ((double)i, "m" + i)).ToArray();
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(7, "Markers", "Markers", 1, 0, "string")
                .SamplesString(7, markers)
                .ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);
            Assert.Equal(XdfReader.MaxMarkersPerStream, s.Markers.Count);
            Assert.True(s.MarkersTruncated);
        }
    }
}
```

Create `QuickLook.Plugin.XdfViewer.Tests/XdfReaderRobustnessTests.cs`:
```csharp
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class XdfReaderRobustnessTests
    {
        [Fact]
        public void TruncatedTrailingBytes_ReturnsPartialWithFlag()
        {
            var bytes = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeader(1, "BioSemi", "EEG", 5, 512, "float32")
                // dangling chunk header claiming far more bytes than remain
                .Raw(new byte[] { 0x04, 0xFF, 0xFF, 0xFF, 0x7F, 0x02, 0x00 })
                .ToStream();

            var file = XdfReader.Read(bytes);
            Assert.True(file.IsTruncated);
            Assert.Single(file.Streams); // the good StreamHeader still parsed
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter "XdfReaderMarkerTests|XdfReaderRobustnessTests" -c Debug`
Expected: FAIL — markers not decoded; (truncation test may already pass from Task 5's guard — that is fine, keep it as a regression guard).

- [ ] **Step 3: Decode marker samples**

Replace `ParseSamples` in `XdfReader.cs` with:
```csharp
        private static void ParseSamples(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            long numSamples = reader.ReadVarLength();
            var s = GetOrAdd(streams, order, id);
            s.TalliedSampleCount += numSamples;

            if (!s.IsMarkerStream) return; // never decode numeric sample values

            var stream = reader.BaseStream;
            for (long i = 0; i < numSamples && stream.Position < contentEnd; i++)
            {
                byte tsMarker = reader.ReadByte();
                double ts = double.NaN;
                if (tsMarker == 8) ts = reader.ReadDouble();

                string value = null;
                int channelCount = s.ChannelCount > 0 ? s.ChannelCount : 1;
                for (int c = 0; c < channelCount; c++)
                {
                    long slen = reader.ReadVarLength();
                    var strBytes = reader.ReadBytes((int)slen);
                    var str = Encoding.UTF8.GetString(strBytes);
                    value = c == 0 ? str : value + " | " + str;
                }

                if (s.Markers.Count < MaxMarkersPerStream)
                    s.Markers.Add(new XdfMarker { Timestamp = ts, Value = value });
                else { s.MarkersTruncated = true; break; }
            }
        }
```

(No other change is needed for robustness — the existing `len < 2 || contentEnd > stream.Length` guard plus the `try/catch` around the loop already produce `IsTruncated = true` with partial results.)

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug`
Expected: PASS (all suites so far).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Xdf/XdfReader.cs QuickLook.Plugin.XdfViewer.Tests/XdfReaderMarkerTests.cs QuickLook.Plugin.XdfViewer.Tests/XdfReaderRobustnessTests.cs
git commit -m "feat: XDF reader decodes capped marker previews; hardens truncation handling"
```

---

### Task 9: ViewModels

**Files:**
- Create: `QuickLook.Plugin.XdfViewer/ViewModels/StreamCardViewModel.cs`
- Create: `QuickLook.Plugin.XdfViewer/ViewModels/XdfOverviewViewModel.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/ViewModelTests.cs`

**Interfaces:**
- Consumes: `XdfFile`, `XdfStream`.
- Produces (namespace `QuickLook.Plugin.XdfViewer.ViewModels`):
  - `StreamCardViewModel(XdfStream s)` with string props `Title`, `ChannelsLine`, `RateLine`, `DurationLine`, `ExpanderHeader`; bool `IsIncomplete`, `IsMarkerStream`; `IReadOnlyList<XdfChannel> Channels`; `IReadOnlyList<XdfMarker> Markers`; `bool MarkersTruncated`.
  - `XdfOverviewViewModel(XdfFile file, string fileName)` with `string FileName`, `string Summary`, `bool IsTruncated`, `ObservableCollection<StreamCardViewModel> Streams`.

- [ ] **Step 1: Write the failing test**

Create `QuickLook.Plugin.XdfViewer.Tests/ViewModelTests.cs`:
```csharp
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class ViewModelTests
    {
        [Fact]
        public void RegularStreamCard_FormatsLines()
        {
            var s = new XdfStream
            {
                Name = "BioSemi", Type = "EEG", ChannelCount = 5,
                ChannelFormat = "float32", NominalSampleRate = 512,
                FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 32512
            };
            var vm = new StreamCardViewModel(s);
            Assert.Equal("BioSemi · EEG", vm.Title);
            Assert.Equal("5 ch · float32", vm.ChannelsLine);
            Assert.Equal("512 Hz · 32,512 samples", vm.RateLine);
            Assert.StartsWith("63.5 s", vm.DurationLine);
            Assert.False(vm.IsIncomplete);
        }

        [Fact]
        public void IrregularMarkerCard_UsesIrregularLabel()
        {
            var s = new XdfStream
            {
                Name = "Markers", Type = "Markers", ChannelCount = 1,
                ChannelFormat = "string", NominalSampleRate = 0, SampleCount = 18
            };
            var vm = new StreamCardViewModel(s);
            Assert.Equal("irregular · 18 samples", vm.RateLine);
            Assert.True(vm.IsMarkerStream);
        }

        [Fact]
        public void Overview_BuildsSummaryAndCards()
        {
            var file = new XdfFile { Version = "1.0" };
            file.Streams.Add(new XdfStream { Name = "A", Type = "EEG", ChannelFormat = "float32",
                                             ChannelCount = 5, NominalSampleRate = 512,
                                             FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 10 });
            var vm = new XdfOverviewViewModel(file, "rec.xdf");
            Assert.Equal("rec.xdf", vm.FileName);
            Assert.Single(vm.Streams);
            Assert.Contains("1 stream", vm.Summary);
            Assert.Contains("XDF v1.0", vm.Summary);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter ViewModelTests -c Debug`
Expected: FAIL — ViewModels do not exist.

- [ ] **Step 3: Implement the ViewModels**

Create `QuickLook.Plugin.XdfViewer/ViewModels/StreamCardViewModel.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class StreamCardViewModel
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public StreamCardViewModel(XdfStream s)
        {
            var name = string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name;
            Title = string.IsNullOrEmpty(s.Type) ? name : $"{name} · {s.Type}";

            var fmt = string.IsNullOrEmpty(s.ChannelFormat) ? "?" : s.ChannelFormat;
            ChannelsLine = $"{s.ChannelCount} ch · {fmt}";

            var count = s.SampleCount.HasValue ? s.SampleCount.Value.ToString("N0", Ci) : "—";
            RateLine = s.NominalSampleRate > 0
                ? $"{FormatRate(s.NominalSampleRate)} · {count} samples"
                : $"irregular · {count} samples";

            if (s.Duration.HasValue)
            {
                DurationLine = $"{s.Duration.Value.ToString("0.0", Ci)} s";
                if (s.FirstTimestamp.HasValue && s.LastTimestamp.HasValue)
                    DurationLine += $" ({s.FirstTimestamp.Value.ToString("0.00", Ci)}–{s.LastTimestamp.Value.ToString("0.00", Ci)})";
            }
            else DurationLine = "duration unknown";

            IsIncomplete = s.IsIncomplete;
            IsMarkerStream = s.IsMarkerStream;
            Channels = s.Channels;
            Markers = s.Markers;
            MarkersTruncated = s.MarkersTruncated;

            ExpanderHeader = IsMarkerStream
                ? (s.Markers.Count > 0 ? $"{s.Markers.Count}{(MarkersTruncated ? "+" : "")} events" : "events")
                : (s.Channels.Count > 0 ? $"{s.Channels.Count} channels" : "channels");
        }

        public string Title { get; }
        public string ChannelsLine { get; }
        public string RateLine { get; }
        public string DurationLine { get; }
        public string ExpanderHeader { get; }
        public bool IsIncomplete { get; }
        public bool IsMarkerStream { get; }
        public IReadOnlyList<XdfChannel> Channels { get; }
        public IReadOnlyList<XdfMarker> Markers { get; }
        public bool MarkersTruncated { get; }

        private static string FormatRate(double hz)
            => (hz >= 1000 ? (hz / 1000.0).ToString("0.###", Ci) + " kHz"
                           : hz.ToString("0.###", Ci) + " Hz");
    }
}
```

Create `QuickLook.Plugin.XdfViewer/ViewModels/XdfOverviewViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.ViewModels
{
    public sealed class XdfOverviewViewModel
    {
        public XdfOverviewViewModel(XdfFile file, string fileName)
        {
            FileName = fileName;
            IsTruncated = file.IsTruncated;
            Streams = new ObservableCollection<StreamCardViewModel>();
            foreach (var s in file.Streams)
                Streams.Add(new StreamCardViewModel(s));

            var sb = new StringBuilder();
            int n = file.Streams.Count;
            sb.Append(n).Append(n == 1 ? " stream" : " streams");
            if (file.TotalDuration.HasValue)
                sb.Append(" · ").Append(file.TotalDuration.Value.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
            if (!string.IsNullOrEmpty(file.Version))
                sb.Append(" · XDF v").Append(file.Version);
            if (IsTruncated)
                sb.Append(" · file appears truncated");
            Summary = sb.ToString();
        }

        public string FileName { get; }
        public string Summary { get; }
        public bool IsTruncated { get; }
        public ObservableCollection<StreamCardViewModel> Streams { get; }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter ViewModelTests -c Debug`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/ViewModels/ QuickLook.Plugin.XdfViewer.Tests/ViewModelTests.cs
git commit -m "feat: overview + stream-card view models with display formatting"
```

---

### Task 10: Plugin.cs (IViewer) + XdfPreviewPanel (WPF UI)

**Files:**
- Create: `QuickLook.Plugin.XdfViewer/Plugin.cs`
- Create: `QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml`
- Create: `QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml.cs`
- Test: `QuickLook.Plugin.XdfViewer.Tests/PluginCanHandleTests.cs`

**Interfaces:**
- Consumes: `XdfReader`, `XdfOverviewViewModel`, `ContextObject`/`IViewer` from `QuickLook.Common`.
- Produces: `class Plugin : IViewer` with `Priority => 0`, `CanHandle(path)` true for `*.xdf`; `Prepare` sets `PreferredSize = new Size(750, 550)`; `View` loads the model off-thread and assigns the panel. `XdfPreviewPanel : UserControl` with `void ShowModel(XdfOverviewViewModel vm)` and `void ShowError(string message)`.

- [ ] **Step 1: Write the failing test (CanHandle only — the pure, unit-testable slice)**

Create `QuickLook.Plugin.XdfViewer.Tests/PluginCanHandleTests.cs`:
```csharp
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class PluginCanHandleTests
    {
        [Theory]
        [InlineData(@"C:\rec\a.xdf", true)]
        [InlineData(@"C:\rec\a.XDF", true)]
        [InlineData(@"C:\rec\a.xdfz", false)]   // compressed variant deferred
        [InlineData(@"C:\rec\a.h5", false)]
        [InlineData(@"C:\rec\a.txt", false)]
        public void CanHandle_MatchesXdfExtension(string path, bool expected)
        {
            Assert.Equal(expected, new Plugin().CanHandle(path));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter PluginCanHandleTests -c Debug`
Expected: FAIL — `Plugin` does not exist.

- [ ] **Step 3: Implement Plugin.cs**

Create `QuickLook.Plugin.XdfViewer/Plugin.cs`:
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.ViewModels;

namespace QuickLook.Plugin.XdfViewer
{
    public class Plugin : IViewer
    {
        private XdfPreviewPanel _panel;

        public int Priority => 0;

        public void Init() { }

        public bool CanHandle(string path)
        {
            return !Directory.Exists(path)
                   && path.EndsWith(".xdf", StringComparison.OrdinalIgnoreCase);
        }

        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size(750, 550);
        }

        public void View(string path, ContextObject context)
        {
            _panel = new XdfPreviewPanel();
            context.ViewerContent = _panel;
            context.Title = Path.GetFileName(path);

            Task.Run(() =>
            {
                try
                {
                    var file = XdfReader.Read(path);
                    var vm = file == null
                        ? null
                        : new XdfOverviewViewModel(file, Path.GetFileName(path));
                    _panel.Dispatcher.Invoke(() =>
                    {
                        _panel.ShowModel(vm);
                        context.IsBusy = false;
                    });
                }
                catch (Exception ex)
                {
                    _panel.Dispatcher.Invoke(() =>
                    {
                        _panel.ShowError(ex.Message);
                        context.IsBusy = false;
                    });
                }
            });
        }

        public void Cleanup()
        {
            _panel = null;
        }
    }
}
```

- [ ] **Step 4: Run to verify CanHandle passes (build the plugin too)**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter PluginCanHandleTests -c Debug`
Expected: FAIL to compile until Step 5 exists (Plugin references `XdfPreviewPanel`). Proceed to Step 5, then re-run — expected PASS (5 cases).

- [ ] **Step 5: Implement the WPF panel**

Create `QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml`:
```xml
<UserControl x:Class="QuickLook.Plugin.XdfViewer.XdfPreviewPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="Transparent">
  <Grid Margin="16">
    <!-- Busy / error / content are toggled from code-behind -->
    <TextBlock x:Name="BusyText" Text="Reading XDF…" Opacity="0.6"
               VerticalAlignment="Center" HorizontalAlignment="Center"/>
    <TextBlock x:Name="ErrorText" Visibility="Collapsed" TextWrapping="Wrap"
               VerticalAlignment="Center" HorizontalAlignment="Center" Opacity="0.8"/>
    <DockPanel x:Name="Root" Visibility="Collapsed">
      <StackPanel DockPanel.Dock="Top" Margin="0,0,0,12">
        <TextBlock x:Name="FileNameText" FontSize="18" FontWeight="SemiBold"
                   TextTrimming="CharacterEllipsis"/>
        <TextBlock x:Name="SummaryText" Opacity="0.7" Margin="0,2,0,0"/>
      </StackPanel>
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ItemsControl x:Name="StreamsList">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Border Width="330" Margin="0,0,12,12" Padding="12" CornerRadius="8"
                      BorderThickness="1" BorderBrush="#33888888" Background="#11888888">
                <StackPanel>
                  <DockPanel>
                    <Border DockPanel.Dock="Right" Visibility="{Binding IsIncomplete,
                              Converter={StaticResource BoolToVis}}"
                            Background="#33CC7722" CornerRadius="4" Padding="4,1" Margin="6,0,0,0">
                      <TextBlock Text="incomplete" FontSize="10" Opacity="0.9"/>
                    </Border>
                    <TextBlock Text="{Binding Title}" FontWeight="SemiBold"
                               TextTrimming="CharacterEllipsis"/>
                  </DockPanel>
                  <TextBlock Text="{Binding ChannelsLine}" Opacity="0.8" Margin="0,6,0,0"/>
                  <TextBlock Text="{Binding RateLine}" Opacity="0.8"/>
                  <TextBlock Text="{Binding DurationLine}" Opacity="0.8"/>
                  <Expander Header="{Binding ExpanderHeader}" Margin="0,6,0,0">
                    <Grid>
                      <!-- Channels (numeric streams) -->
                      <ItemsControl ItemsSource="{Binding Channels}" MaxHeight="180"
                                    Visibility="{Binding IsMarkerStream,
                                      Converter={StaticResource InverseBoolToVis}}">
                        <ItemsControl.Template>
                          <ControlTemplate>
                            <ScrollViewer VerticalScrollBarVisibility="Auto">
                              <ItemsPresenter/>
                            </ScrollViewer>
                          </ControlTemplate>
                        </ItemsControl.Template>
                        <ItemsControl.ItemTemplate>
                          <DataTemplate>
                            <TextBlock Opacity="0.85">
                              <Run Text="{Binding Label, Mode=OneWay}"/>
                              <Run Text="{Binding Unit, Mode=OneWay}" FontStyle="Italic" Foreground="#888"/>
                            </TextBlock>
                          </DataTemplate>
                        </ItemsControl.ItemTemplate>
                      </ItemsControl>
                      <!-- Markers (string streams) -->
                      <ItemsControl ItemsSource="{Binding Markers}" MaxHeight="180"
                                    Visibility="{Binding IsMarkerStream,
                                      Converter={StaticResource BoolToVis}}">
                        <ItemsControl.Template>
                          <ControlTemplate>
                            <ScrollViewer VerticalScrollBarVisibility="Auto">
                              <ItemsPresenter/>
                            </ScrollViewer>
                          </ControlTemplate>
                        </ItemsControl.Template>
                        <ItemsControl.ItemTemplate>
                          <DataTemplate>
                            <TextBlock Opacity="0.85">
                              <Run Text="{Binding Timestamp, StringFormat={}{0:0.00}s, Mode=OneWay}" Foreground="#888"/>
                              <Run Text="{Binding Value, Mode=OneWay}"/>
                            </TextBlock>
                          </DataTemplate>
                        </ItemsControl.ItemTemplate>
                      </ItemsControl>
                    </Grid>
                  </Expander>
                </StackPanel>
              </Border>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </DockPanel>
  </Grid>
</UserControl>
```

Create `QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QuickLook.Plugin.XdfViewer.ViewModels;

namespace QuickLook.Plugin.XdfViewer
{
    public partial class XdfPreviewPanel : UserControl
    {
        public XdfPreviewPanel()
        {
            Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
            Resources.Add("InverseBoolToVis", new InverseBooleanToVisibilityConverter());
            InitializeComponent();
        }

        public void ShowModel(XdfOverviewViewModel vm)
        {
            if (vm == null) { ShowError("Couldn't read this XDF file."); return; }
            BusyText.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Collapsed;
            FileNameText.Text = vm.FileName;
            SummaryText.Text = vm.Summary;
            StreamsList.ItemsSource = vm.Streams;
            Root.Visibility = Visibility.Visible;
        }

        public void ShowError(string message)
        {
            BusyText.Visibility = Visibility.Collapsed;
            Root.Visibility = Visibility.Collapsed;
            ErrorText.Text = "Couldn't read this XDF file.\n" + message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type t, object p, System.Globalization.CultureInfo c)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, System.Type t, object p, System.Globalization.CultureInfo c)
            => Binding.DoNothing;
    }
}
```

- [ ] **Step 6: Build the plugin and run the CanHandle test**

Run: `dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Debug`
Expected: `Build succeeded`. (Use `msbuild … /t:Build` if the WPF/net462 build requires it.)
Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests --filter PluginCanHandleTests -c Debug`
Expected: PASS (5 cases).

- [ ] **Step 7: Commit**

```bash
git add QuickLook.Plugin.XdfViewer/Plugin.cs QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml QuickLook.Plugin.XdfViewer/XdfPreviewPanel.xaml.cs QuickLook.Plugin.XdfViewer.Tests/PluginCanHandleTests.cs
git commit -m "feat: IViewer plugin + WPF cards preview panel"
```

---

### Task 11: Packaging + README + optional real-file smoke test

**Files:**
- Create: `README.md`
- Create: `QuickLook.Plugin.XdfViewer.Tests/RealFileSmokeTests.cs`
- Verify: `QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1` produces the `.qlplugin`

**Interfaces:**
- Consumes: the whole plugin + `XdfReader`.
- Produces: `QuickLook.Plugin.XdfViewer.qlplugin` and developer-facing docs. The real-file test is skipped automatically when the (gitignored) sample recording is absent.

- [ ] **Step 1: Add an opt-in real-file smoke test**

Create `QuickLook.Plugin.XdfViewer.Tests/RealFileSmokeTests.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class RealFileSmokeTests
    {
        // The sample recording is gitignored; only present on the developer's machine.
        private static string SamplePath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                var hit = Directory.GetFiles(dir, "*.xdf", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (hit != null) return hit;
            }
            return null;
        }

        [Fact]
        public void RealFile_ParsesStreamsWhenPresent()
        {
            var path = SamplePath();
            if (path == null) return; // skipped when the sample is absent

            var file = XdfReader.Read(path);
            Assert.NotNull(file);
            Assert.NotEmpty(file.Streams);
            Assert.All(file.Streams, s => Assert.True(s.ChannelCount >= 0));
        }
    }
}
```

- [ ] **Step 2: Run the full suite**

Run: `dotnet test QuickLook.Plugin.XdfViewer.Tests -c Debug`
Expected: PASS (all suites). If the sample `.xdf` sits at the repo root, `RealFile_ParsesStreamsWhenPresent` exercises it; otherwise it no-ops.

- [ ] **Step 3: Produce the .qlplugin**

Run:
```bash
dotnet build QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.csproj -c Release
pwsh QuickLook.Plugin.XdfViewer/Scripts/pack-zip.ps1 -Configuration Release
```
Expected: `QuickLook.Plugin.XdfViewer/QuickLook.Plugin.XdfViewer.qlplugin` exists (it is gitignored by the `*.qlplugin` rule and not committed).

- [ ] **Step 4: Write the README (includes the manual verification checklist)**

Create `README.md`:
```markdown
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
```

- [ ] **Step 5: Commit**

```bash
git add README.md QuickLook.Plugin.XdfViewer.Tests/RealFileSmokeTests.cs
git commit -m "docs: README + build/pack instructions; add opt-in real-file smoke test"
```

---

## Self-Review

**1. Spec coverage** — every spec section maps to a task:
- Overview-only / hybrid cards → Tasks 9–10. Robust-to-incomplete → Task 7. Per-channel detail → Tasks 5, 10. Marker preview → Task 8. Performance (seek-past samples) → Tasks 7–8 (no numeric decode; resync seek). `IViewer`/`ContextObject` contract → Tasks 1, 10. XDF format (Length−2, tags, little-endian) → Tasks 2, 4, 5–8. Error handling (bad magic, truncation) → Tasks 5, 8, 10. Testing without app launch → Tasks 2–9, 11 (synthetic fixtures + opt-in real file). Packaging → Tasks 1, 11. Defaults (750×550, cap 500, ~32) → Task 10, Global Constraints. `.xdfz` deferred → not implemented (Task 10 CanHandle explicitly rejects `.xdfz`).
- The ~32 channel inline/collapse threshold is realized via the always-present, scrollable Expander (channels are always collapsed behind the Expander, satisfying the intent that large lists don't dominate). No separate task needed.

**2. Placeholder scan** — no TBD/TODO/"add error handling" placeholders; every code step carries complete code and exact commands.

**3. Type consistency** — `XdfReader.Read(Stream|string)`, `MaxMarkersPerStream`, model property names (`SampleCount`, `TalliedSampleCount`, `IsIncomplete`, `MarkersTruncated`, `IsMarkerStream`, `Duration`), `StreamCardViewModel(XdfStream)` and `XdfOverviewViewModel(XdfFile,string)`, `XdfPreviewPanel.Load/ShowModel/ShowError` are used identically across tasks. (Note: `Plugin.View` calls `_panel.ShowModel/ShowError`; the panel exposes exactly those — no `Load` method is actually required, so it was dropped to avoid an unused-method mismatch.)

---

## Execution Handoff

*(Filled in when presenting to the user.)*
