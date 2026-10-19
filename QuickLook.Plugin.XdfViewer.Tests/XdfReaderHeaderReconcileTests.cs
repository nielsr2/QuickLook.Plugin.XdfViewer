using System.Linq;
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>The header's channel metadata and the sample data can disagree. Channel rows must follow
    /// what the samples actually carry, so no row is left without a waveform and no data is hidden.</summary>
    public class XdfReaderHeaderReconcileTests
    {
        private static string Header(string channelCount, string format, string desc) =>
            "<?xml version=\"1.0\"?><info><name>S</name><type>EEG</type>" +
            (channelCount == null ? "" : $"<channel_count>{channelCount}</channel_count>") +
            $"<nominal_srate>100</nominal_srate><channel_format>{format}</channel_format>{desc}</info>";

        private static string Labels(params string[] labels) =>
            "<desc><channels>" + string.Concat(labels.Select(l => $"<channel><label>{l}</label></channel>")) +
            "</channels></desc>";

        [Fact]
        public void MoreLabelsThanChannelCount_RowsFollowChannelCount()
        {
            var bytes = new XdfFixtureBuilder().FileHeader()
                .StreamHeaderXml(1, Header("2", "float32", Labels("A", "B", "C", "D"))).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.Equal(new[] { "A", "B" }, s.Channels.Select(c => c.Label));
        }

        [Fact]
        public void MissingChannelCount_IsTakenFromTheChannelList_AndDecodes()
        {
            var builder = new XdfFixtureBuilder().FileHeader()
                .StreamHeaderXml(1, Header(null, "float32", Labels("A", "B")))
                .SamplesNumeric(1, "float32", 2, false, new double[] { 1, 2 }, new double[] { 3, 4 });

            var file = XdfReader.Read(builder.ToStream());
            var s = Assert.Single(file.Streams);
            Assert.Equal(2, s.ChannelCount);

            var vm = new XdfOverviewViewModel(file, "f.xdf");
            vm.ApplyEnvelopes(XdfReader.FillEnvelopes(file, builder.ToStream(), new EnvelopeOptions { Buckets = 2 }));
            Assert.All(Assert.Single(vm.Streams).Channels, c => Assert.True(c.HasWaveform));
        }

        [Fact]
        public void RepeatedStreamHeader_DoesNotDuplicateChannels()
        {
            var bytes = new XdfFixtureBuilder().FileHeader()
                .StreamHeaderXml(1, Header("2", "float32", Labels("A", "B")))
                .StreamHeaderXml(1, Header("2", "float32", Labels("A", "B"))).ToStream();

            var s = Assert.Single(XdfReader.Read(bytes).Streams);

            Assert.Equal(new[] { "A", "B" }, s.Channels.Select(c => c.Label));
        }

        [Fact]
        public void FooterWithoutSampleCount_IsNotFlaggedIncomplete()
        {
            var withFooter = new XdfFixtureBuilder().FileHeader()
                .StreamHeaderXml(1, Header("1", "float32", ""))
                .SamplesNumeric(1, "float32", 1, false, new double[] { 1 }, new double[] { 2 })
                .StreamFooterXml(1, "<?xml version=\"1.0\"?><info><first_timestamp>0</first_timestamp>" +
                                    "<last_timestamp>0.01</last_timestamp></info>")
                .ToStream();

            var s = XdfReader.Read(withFooter).Streams.Single(x => x.Id == 1);

            Assert.False(s.IsIncomplete);
            Assert.Equal(2, s.SampleCount);
        }
    }
}
