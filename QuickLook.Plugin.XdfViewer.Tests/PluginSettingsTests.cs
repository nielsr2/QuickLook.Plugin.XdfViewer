using System;
using System.Windows.Media;
using QuickLook.Plugin.XdfViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// ResolveAccentColor is a pure function and is tested exhaustively here. WaveformColorTheme
    /// and CustomWaveformColor's get/set are backed by SettingHelper, which persists to a real
    /// file under %AppData%\pooi.moe\QuickLook\&lt;domain&gt;.config and caches loaded XmlDocuments in
    /// a static in-process dictionary; round-tripping those against the real domain here would
    /// pollute a real user's settings file and risk cross-test interference, so that is deliberately
    /// not tested.
    /// </summary>
    public class PluginSettingsTests
    {
        [Fact]
        public void ResolveAccentColor_Mono_IsNull()
        {
            Assert.Null(PluginSettings.ResolveAccentColor(WaveformColorTheme.Mono));
        }

        [Fact]
        public void ResolveAccentColor_Ocean_MatchesDocumentedRgb()
        {
            Assert.Equal(Color.FromRgb(0x2F, 0xB8, 0xC6), PluginSettings.ResolveAccentColor(WaveformColorTheme.Ocean));
        }

        [Fact]
        public void ResolveAccentColor_Sunset_MatchesDocumentedRgb()
        {
            Assert.Equal(Color.FromRgb(0xF2, 0xA3, 0x3C), PluginSettings.ResolveAccentColor(WaveformColorTheme.Sunset));
        }

        [Fact]
        public void ResolveAccentColor_Forest_MatchesDocumentedRgb()
        {
            Assert.Equal(Color.FromRgb(0x59, 0xC9, 0x7A), PluginSettings.ResolveAccentColor(WaveformColorTheme.Forest));
        }

        [Fact]
        public void ResolveAccentColor_Berry_MatchesDocumentedRgb()
        {
            Assert.Equal(Color.FromRgb(0xC8, 0x68, 0xE0), PluginSettings.ResolveAccentColor(WaveformColorTheme.Berry));
        }

        [Fact]
        public void ResolveAccentColor_Purple_MatchesDocumentedRgb()
        {
            Assert.Equal(Color.FromRgb(0x8A, 0x5C, 0xF6), PluginSettings.ResolveAccentColor(WaveformColorTheme.Purple));
        }

        [Fact]
        public void ResolveAccentColor_Custom_ReturnsNonNullColor()
        {
            // Deliberately not asserting an exact color: CustomWaveformColor is backed by
            // SettingHelper against the real config file, and a prior run or the developer's
            // own machine may have already persisted a different custom color there.
            Assert.NotNull(PluginSettings.ResolveAccentColor(WaveformColorTheme.Custom));
        }

        [Fact]
        public void WaveformColorTheme_DeclaresExactlySevenValuesInOrder()
        {
            var expected = new[]
            {
                WaveformColorTheme.Mono,
                WaveformColorTheme.Ocean,
                WaveformColorTheme.Sunset,
                WaveformColorTheme.Forest,
                WaveformColorTheme.Berry,
                WaveformColorTheme.Purple,
                WaveformColorTheme.Custom
            };

            var actual = (WaveformColorTheme[])Enum.GetValues(typeof(WaveformColorTheme));

            Assert.Equal(expected, actual);
        }
    }
}
