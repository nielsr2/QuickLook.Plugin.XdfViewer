using System.Collections.Generic;
using System.ComponentModel;
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class ChannelViewModelTests
    {
        private static ChannelEnvelope MakeEnvelope(bool empty = false) => new ChannelEnvelope
        {
            Min = new[] { 0f, 1f },
            Max = new[] { 2f, 3f },
            Filled = new[] { true, true },
            BucketCount = 2,
            DataMin = 0f,
            DataMax = 3f,
            IsApproximate = false,
            IsEmpty = empty
        };

        private static List<string> CapturePropertyChanges(INotifyPropertyChanged source)
        {
            var names = new List<string>();
            source.PropertyChanged += (s, e) => names.Add(e.PropertyName);
            return names;
        }

        [Fact]
        public void LabelAndUnit_PassThroughFromChannel()
        {
            var channel = new XdfChannel { Label = "Fp1", Unit = "uV", Type = "EEG" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);

            Assert.Equal("Fp1", vm.Label);
            Assert.Equal("uV", vm.Unit);
        }

        [Fact]
        public void StreamDurationSeconds_ComesFromStream()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream { FirstTimestamp = 0, LastTimestamp = 10.5 };
            var vm = new ChannelViewModel(channel, stream);

            Assert.Equal(10.5, vm.StreamDurationSeconds);
        }

        [Fact]
        public void StreamFirstTimestamp_ComesFromStream()
        {
            // The full-view marker overlay anchors marker times to this stream's own first timestamp.
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream { FirstTimestamp = 123.5, LastTimestamp = 130.0 };
            var vm = new ChannelViewModel(channel, stream);

            Assert.Equal(123.5, vm.StreamFirstTimestamp);
        }

        [Fact]
        public void HasWaveform_FalseInitially_NoSparkline()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);

            Assert.Null(vm.Sparkline);
            Assert.False(vm.HasWaveform);
        }

        [Fact]
        public void RefreshEnvelope_SetsSparkline_RaisesPropertyChanged_HasWaveformTrue()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);
            var raised = CapturePropertyChanges(vm);

            channel.Envelope = MakeEnvelope(empty: false);
            vm.RefreshEnvelope();

            Assert.Same(channel.Envelope, vm.Sparkline);
            Assert.True(vm.HasWaveform);
            Assert.Contains(nameof(ChannelViewModel.Sparkline), raised);
            Assert.Contains(nameof(ChannelViewModel.HasWaveform), raised);
        }

        [Fact]
        public void RefreshEnvelope_EmptyEnvelope_HasWaveformFalse()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);

            channel.Envelope = MakeEnvelope(empty: true);
            vm.RefreshEnvelope();

            Assert.False(vm.HasWaveform);
        }

        private static ChannelEnvelope MakeFlatEnvelope() => new ChannelEnvelope
        {
            Min = new[] { 0f, 0f },
            Max = new[] { 0f, 0f },
            Filled = new[] { true, true },
            BucketCount = 2,
            DataMin = 0f,
            DataMax = 0f,
            IsApproximate = false,
            IsEmpty = false
        };

        [Fact]
        public void IsFlat_FalseWhenNoSparkline()
        {
            var vm = new ChannelViewModel(new XdfChannel { Label = "x" }, new XdfStream());
            Assert.False(vm.IsFlat);
        }

        [Fact]
        public void IsFlat_TrueForConstantChannel_RaisesPropertyChanged()
        {
            var channel = new XdfChannel { Label = "accX" };
            var vm = new ChannelViewModel(channel, new XdfStream());
            var raised = CapturePropertyChanges(vm);

            channel.Envelope = MakeFlatEnvelope();
            vm.RefreshEnvelope();

            Assert.True(vm.IsFlat);
            Assert.Contains(nameof(ChannelViewModel.IsFlat), raised);
        }

        [Fact]
        public void IsFlat_FalseForVaryingChannel()
        {
            var channel = new XdfChannel { Label = "accX" };
            var vm = new ChannelViewModel(channel, new XdfStream());

            channel.Envelope = MakeEnvelope(empty: false); // DataMin 0, DataMax 3
            vm.RefreshEnvelope();

            Assert.False(vm.IsFlat);
        }

        [Fact]
        public void IsFlat_FalseForEmptyChannel()
        {
            var channel = new XdfChannel { Label = "accX" };
            var vm = new ChannelViewModel(channel, new XdfStream());

            channel.Envelope = MakeEnvelope(empty: true);
            vm.RefreshEnvelope();

            Assert.False(vm.IsFlat);
        }

        /// <summary>A sample counter (nSeq-shaped): a straight climb, one step per bucket.</summary>
        private static ChannelEnvelope MakeCounterEnvelope()
        {
            const int n = 12;
            var min = new float[n];
            var max = new float[n];
            var filled = new bool[n];
            for (int b = 0; b < n; b++) { min[b] = b; max[b] = b + 0.9f; filled[b] = true; }

            return new ChannelEnvelope
            {
                Min = min, Max = max, Filled = filled, BucketCount = n,
                DataMin = 0f, DataMax = n - 1 + 0.9f
            };
        }

        [Fact]
        public void IsCounter_FalseWhenNoSparkline()
        {
            var vm = new ChannelViewModel(new XdfChannel { Label = "nSeq" }, new XdfStream());
            Assert.False(vm.IsCounter);
        }

        [Fact]
        public void IsCounter_TrueForMonotonicRamp_RaisesPropertyChanged()
        {
            var channel = new XdfChannel { Label = "nSeq" };
            var vm = new ChannelViewModel(channel, new XdfStream());
            var raised = CapturePropertyChanges(vm);

            vm.ApplyEnvelope(MakeCounterEnvelope());

            Assert.True(vm.IsCounter);
            Assert.Contains(nameof(ChannelViewModel.IsCounter), raised);
        }

        [Fact]
        public void IsCounter_FalseForFlatChannel()
        {
            var channel = new XdfChannel { Label = "accX" };
            var vm = new ChannelViewModel(channel, new XdfStream());

            vm.ApplyEnvelope(MakeFlatEnvelope());

            Assert.False(vm.IsCounter);
            Assert.True(vm.IsFlat);            // the two badges are mutually exclusive
        }

        [Fact]
        public void IsCounter_FalseForNoisyChannel()
        {
            var channel = new XdfChannel { Label = "RESPIRATION0" };
            var vm = new ChannelViewModel(channel, new XdfStream());

            vm.ApplyEnvelope(MakeEnvelope(empty: false));

            Assert.False(vm.IsCounter);
        }

        [Fact]
        public void IsExpanded_SetTrue_RaisesPropertyChanged()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);
            var raised = CapturePropertyChanges(vm);

            Assert.False(vm.IsExpanded);
            vm.IsExpanded = true;

            Assert.True(vm.IsExpanded);
            Assert.Contains(nameof(ChannelViewModel.IsExpanded), raised);
        }

        [Fact]
        public void IsFilteredOut_SetTrue_RaisesPropertyChanged()
        {
            var vm = new ChannelViewModel(new XdfChannel { Label = "Fp1" }, new XdfStream());
            var raised = CapturePropertyChanges(vm);

            Assert.False(vm.IsFilteredOut);
            vm.IsFilteredOut = true;

            Assert.True(vm.IsFilteredOut);
            Assert.Contains(nameof(ChannelViewModel.IsFilteredOut), raised);
        }

        [Fact]
        public void StreamCard_NoChannelMatch_DefaultsFalse_RaisesOnSet()
        {
            var stream = new XdfStream { ChannelFormat = "float32" };
            stream.Channels.Add(new XdfChannel { Label = "Fp1" });
            var card = new StreamCardViewModel(stream);
            var raised = new List<string>();
            card.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            Assert.False(card.NoChannelMatch);
            card.SetNoChannelMatch(true);

            Assert.True(card.NoChannelMatch);
            Assert.Contains(nameof(StreamCardViewModel.NoChannelMatch), raised);
        }

        [Fact]
        public void SetDetail_SetsDetail_RaisesPropertyChanged()
        {
            var channel = new XdfChannel { Label = "Fp1" };
            var stream = new XdfStream();
            var vm = new ChannelViewModel(channel, stream);
            var raised = CapturePropertyChanges(vm);

            var detail = MakeEnvelope();
            vm.SetDetail(detail);

            Assert.Same(detail, vm.Detail);
            Assert.Contains(nameof(ChannelViewModel.Detail), raised);
        }
    }
}
