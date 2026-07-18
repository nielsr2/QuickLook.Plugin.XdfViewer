using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class ChannelEnvelopeTests
    {
        [Fact]
        public void XdfChannel_Envelope_IsNullByDefault()
        {
            var channel = new XdfChannel();
            Assert.Null(channel.Envelope);
        }

        [Fact]
        public void ChannelEnvelope_ArraysAreNullByDefault()
        {
            var envelope = new ChannelEnvelope();
            Assert.Null(envelope.Min);
            Assert.Null(envelope.Max);
            Assert.Null(envelope.Filled);
        }

        [Fact]
        public void ChannelEnvelope_IsEmptyIsFalseByDefault()
        {
            var envelope = new ChannelEnvelope();
            Assert.False(envelope.IsEmpty);
        }

        [Fact]
        public void ChannelEnvelope_ExposesSetValues()
        {
            var min = new float[] { 0.1f, 0.2f, 0.3f };
            var max = new float[] { 1.1f, 1.2f, 1.3f };
            var filled = new bool[] { true, false, true };

            var envelope = new ChannelEnvelope
            {
                Min = min,
                Max = max,
                Filled = filled,
                BucketCount = 42,
                DataMin = -10.5f,
                DataMax = 50.7f,
                IsApproximate = true,
                IsEmpty = true
            };

            Assert.Same(min, envelope.Min);
            Assert.Same(max, envelope.Max);
            Assert.Same(filled, envelope.Filled);
            Assert.Equal(42, envelope.BucketCount);
            Assert.Equal(-10.5f, envelope.DataMin);
            Assert.Equal(50.7f, envelope.DataMax);
            Assert.True(envelope.IsApproximate);
            Assert.True(envelope.IsEmpty);
        }

        [Fact]
        public void StreamEnvelope_ChannelsAreNullByDefault()
        {
            var streamEnvelope = new StreamEnvelope();
            Assert.Null(streamEnvelope.Channels);
        }

        [Fact]
        public void StreamEnvelope_ExposesSetValues()
        {
            var channels = new[] { new ChannelEnvelope(), new ChannelEnvelope() };

            var streamEnvelope = new StreamEnvelope
            {
                StreamId = 123,
                Channels = channels,
                IsApproximate = true
            };

            Assert.Equal(123u, streamEnvelope.StreamId);
            Assert.Same(channels, streamEnvelope.Channels);
            Assert.True(streamEnvelope.IsApproximate);
        }
    }
}
