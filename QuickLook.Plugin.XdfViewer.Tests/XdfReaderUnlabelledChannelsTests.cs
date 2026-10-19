using System.Linq;
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>Some outlets (e.g. g.tec Unicorn) write an empty <c>&lt;desc /&gt;</c> with no channel
    /// list. The declared channel_count still holds, so the stream must still get one row per channel.</summary>
    public class XdfReaderUnlabelledChannelsTests
    {
        private static string Header(int channelCount, string desc) =>
            "<?xml version=\"1.0\"?><info><name>UN</name><type>Data</type>" +
            $"<channel_count>{channelCount}</channel_count><nominal_srate>250</nominal_srate>" +
            $"<channel_format>float32</channel_format>{desc}</info>";

        [Fact]
        public void EmptyDesc_CreatesPlaceholderChannelsUpToDeclaredCount()
        {
            var bytes = new XdfFixtureBuilder().FileHeader().StreamHeaderXml(1, Header(3, "<desc />")).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.Equal(new[] { "ch 1", "ch 2", "ch 3" }, s.Channels.Select(c => c.Label));
        }

        [Fact]
        public void PartialChannelList_IsPaddedAfterTheDeclaredChannels()
        {
            var desc = "<desc><channels><channel><label>Fp1</label><unit>uV</unit></channel></channels></desc>";
            var bytes = new XdfFixtureBuilder().FileHeader().StreamHeaderXml(1, Header(3, desc)).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.Equal(new[] { "Fp1", "ch 2", "ch 3" }, s.Channels.Select(c => c.Label));
            Assert.Equal("uV", s.Channels[0].Unit);
        }

        [Fact]
        public void BlankLabel_FallsBackToChannelNumber()
        {
            var desc = "<desc><channels><channel><label>Fp1</label></channel>" +
                       "<channel><label /></channel><channel><label>  </label></channel></channels></desc>";
            var bytes = new XdfFixtureBuilder().FileHeader().StreamHeaderXml(1, Header(3, desc)).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.Equal(new[] { "Fp1", "ch 2", "ch 3" }, s.Channels.Select(c => c.Label));
        }

        [Fact]
        public void AbsurdChannelCount_DoesNotPadBeyondTheCap()
        {
            var bytes = new XdfFixtureBuilder().FileHeader().StreamHeaderXml(1, Header(10_000_000, "<desc />")).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.True(s.Channels.Count <= XdfReader.MaxEnvelopeChannels);
        }

        [Fact]
        public void EmptyDesc_SparklinesReachTheChannelRows()
        {
            var builder = new XdfFixtureBuilder()
                .FileHeader()
                .StreamHeaderXml(1, Header(2, "<desc />"))
                .SamplesNumeric(1, "float32", 2, false,
                    new double[] { 0, 5 }, new double[] { 10, -5 }, new double[] { -5, 3 });

            var file = XdfReader.Read(builder.ToStream());
            var vm = new XdfOverviewViewModel(file, "f.xdf");
            vm.ApplyEnvelopes(XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions { Buckets = 2 }));

            var card = Assert.Single(vm.Streams);
            Assert.Equal("2 channels", card.ExpanderHeader);
            Assert.All(card.Channels, c => Assert.True(c.HasWaveform));
        }
    }
}
