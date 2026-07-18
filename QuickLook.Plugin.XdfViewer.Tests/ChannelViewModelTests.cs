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
