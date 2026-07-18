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
        public void StreamCard_ChannelViewModels_ExposeLabelAndUnit()
        {
            var s = new XdfStream
            {
                Name = "BioSemi", Type = "EEG", ChannelCount = 2,
                ChannelFormat = "float32", NominalSampleRate = 512,
                FirstTimestamp = 0, LastTimestamp = 63.5, SampleCount = 32512
            };
            s.Channels.Add(new XdfChannel { Label = "Fp1", Unit = "uV", Type = "EEG" });
            s.Channels.Add(new XdfChannel { Label = "Fp2", Unit = "uV", Type = "EEG" });

            var vm = new StreamCardViewModel(s);

            Assert.Equal(2, vm.Channels.Count);
            Assert.Equal("Fp1", vm.Channels[0].Label);
            Assert.Equal("uV", vm.Channels[0].Unit);
            Assert.Equal("Fp2", vm.Channels[1].Label);
            Assert.Equal("2 channels", vm.ExpanderHeader);
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
