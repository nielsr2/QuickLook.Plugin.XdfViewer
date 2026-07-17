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
            Assert.Contains(file.Streams, s => s.ChannelCount > 0);
        }
    }
}
