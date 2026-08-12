using System;
using System.Windows.Media;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.XdfViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// ThemePalette is pure (colors + a Themes→palette mapping) and is tested exhaustively here.
    /// The registry read that feeds the osAppsUseDarkTheme flag stays in XdfPreviewPanel.ApplyTheme,
    /// so these tests never touch the OS theme state.
    /// </summary>
    public class ThemePaletteTests
    {
        [Fact]
        public void IsDark_DarkTheme_IgnoresOsFlag()
        {
            Assert.True(ThemePalette.IsDark(Themes.Dark, osAppsUseDarkTheme: false));
            Assert.True(ThemePalette.IsDark(Themes.Dark, osAppsUseDarkTheme: true));
        }

        [Fact]
        public void IsDark_LightTheme_IgnoresOsFlag()
        {
            Assert.False(ThemePalette.IsDark(Themes.Light, osAppsUseDarkTheme: false));
            Assert.False(ThemePalette.IsDark(Themes.Light, osAppsUseDarkTheme: true));
        }

        [Fact]
        public void IsDark_NoneTheme_FollowsOsFlag()
        {
            Assert.True(ThemePalette.IsDark(Themes.None, osAppsUseDarkTheme: true));
            Assert.False(ThemePalette.IsDark(Themes.None, osAppsUseDarkTheme: false));
        }

        [Fact]
        public void Resolve_ReturnsTheMatchingSharedInstance()
        {
            Assert.Same(ThemePalette.Dark, ThemePalette.Resolve(Themes.Dark, osAppsUseDarkTheme: false));
            Assert.Same(ThemePalette.Light, ThemePalette.Resolve(Themes.Light, osAppsUseDarkTheme: true));
            Assert.Same(ThemePalette.Dark, ThemePalette.Resolve(Themes.None, osAppsUseDarkTheme: true));
            Assert.Same(ThemePalette.Light, ThemePalette.Resolve(Themes.None, osAppsUseDarkTheme: false));
        }

        [Fact]
        public void DarkPalette_MatchesDocumentedColors()
        {
            var p = ThemePalette.Dark;

            // These first two pin the pre-refactor ApplyTheme constants exactly.
            Assert.Equal(Color.FromRgb(0xEC, 0xEC, 0xEC), p.NeutralForeground);
            Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), p.PanelBackground);
            Assert.Equal(Color.FromArgb(0xCC, 0x00, 0x00, 0x00), p.OverlayChipBackground);
            Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0xFF), p.OverlayChipForeground);
            Assert.Equal(Color.FromArgb(0x66, 0x3F, 0xB9, 0x50), p.SampleBadgeBackground);
            Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0xFF), p.SampleBadgeForeground);
            Assert.Equal(Color.FromArgb(0x40, 0x00, 0x00, 0x00), p.MinimapBackground);
            Assert.Equal(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF), p.MinimapViewportFill);
            Assert.Equal(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF), p.MinimapViewportStroke);
            Assert.Equal(Color.FromRgb(0x88, 0x88, 0x88), p.SecondaryForeground);
            Assert.Equal(Color.FromRgb(0x9A, 0x9A, 0x9A), p.MutedGlyphForeground);
            Assert.Equal(Color.FromArgb(0x99, 0x00, 0x00, 0x00), p.MarkerLabelBackground);
            Assert.Equal(Color.FromRgb(0xE0, 0x8E, 0x2A), p.MarkerLabelForeground);
        }

        [Fact]
        public void LightPalette_MatchesDocumentedColors()
        {
            var p = ThemePalette.Light;

            Assert.Equal(Color.FromRgb(0x1A, 0x1A, 0x1A), p.NeutralForeground);
            Assert.Equal(Color.FromRgb(0xF5, 0xF5, 0xF5), p.PanelBackground);
            Assert.Equal(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF), p.OverlayChipBackground);
            Assert.Equal(Color.FromRgb(0x1A, 0x1A, 0x1A), p.OverlayChipForeground);
            Assert.Equal(Color.FromArgb(0x4D, 0x3F, 0xB9, 0x50), p.SampleBadgeBackground);
            Assert.Equal(Color.FromRgb(0x0F, 0x53, 0x23), p.SampleBadgeForeground);
            Assert.Equal(Color.FromArgb(0x15, 0x00, 0x00, 0x00), p.MinimapBackground);
            Assert.Equal(Color.FromArgb(0x26, 0x00, 0x00, 0x00), p.MinimapViewportFill);
            Assert.Equal(Color.FromArgb(0xAA, 0x33, 0x33, 0x33), p.MinimapViewportStroke);
            Assert.Equal(Color.FromRgb(0x6A, 0x6A, 0x6A), p.SecondaryForeground);
            Assert.Equal(Color.FromRgb(0x76, 0x76, 0x76), p.MutedGlyphForeground);
            Assert.Equal(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF), p.MarkerLabelBackground);
            Assert.Equal(Color.FromRgb(0x8F, 0x50, 0x00), p.MarkerLabelForeground);
        }

        // Contrast guards: keep future value tweaks honest. Chip/badge backgrounds are
        // translucent, so they are composited over the theme's panel first. Thresholds:
        // WCAG-style 4.5:1 for text, 3:1 for the 12px secondary runs and symbol glyphs.

        public static readonly object[][] TextContrastPairs =
        {
            new object[] { "Dark crosshair chip", true, nameof(ThemePalette.OverlayChipForeground), nameof(ThemePalette.OverlayChipBackground) },
            new object[] { "Light crosshair chip", false, nameof(ThemePalette.OverlayChipForeground), nameof(ThemePalette.OverlayChipBackground) },
            new object[] { "Dark sample badge", true, nameof(ThemePalette.SampleBadgeForeground), nameof(ThemePalette.SampleBadgeBackground) },
            new object[] { "Light sample badge", false, nameof(ThemePalette.SampleBadgeForeground), nameof(ThemePalette.SampleBadgeBackground) },
            new object[] { "Dark marker label", true, nameof(ThemePalette.MarkerLabelForeground), nameof(ThemePalette.MarkerLabelBackground) },
            new object[] { "Light marker label", false, nameof(ThemePalette.MarkerLabelForeground), nameof(ThemePalette.MarkerLabelBackground) },
            new object[] { "Dark plain text", true, nameof(ThemePalette.NeutralForeground), nameof(ThemePalette.PanelBackground) },
            new object[] { "Light plain text", false, nameof(ThemePalette.NeutralForeground), nameof(ThemePalette.PanelBackground) },
        };

        [Theory]
        [MemberData(nameof(TextContrastPairs))]
        public void TextPairs_ClearReadabilityThreshold(string label, bool dark, string fgField, string bgField)
        {
            AssertContrast(label, dark, fgField, bgField, minimumRatio: 4.5);
        }

        public static readonly object[][] GlyphContrastPairs =
        {
            new object[] { "Dark secondary runs", true, nameof(ThemePalette.SecondaryForeground), nameof(ThemePalette.PanelBackground) },
            new object[] { "Light secondary runs", false, nameof(ThemePalette.SecondaryForeground), nameof(ThemePalette.PanelBackground) },
            new object[] { "Dark muted glyphs", true, nameof(ThemePalette.MutedGlyphForeground), nameof(ThemePalette.PanelBackground) },
            new object[] { "Light muted glyphs", false, nameof(ThemePalette.MutedGlyphForeground), nameof(ThemePalette.PanelBackground) },
        };

        [Theory]
        [MemberData(nameof(GlyphContrastPairs))]
        public void GlyphPairs_ClearReadabilityThreshold(string label, bool dark, string fgField, string bgField)
        {
            AssertContrast(label, dark, fgField, bgField, minimumRatio: 3.0);
        }

        private static void AssertContrast(string label, bool dark, string fgField, string bgField, double minimumRatio)
        {
            var palette = dark ? ThemePalette.Dark : ThemePalette.Light;
            var fg = GetColor(palette, fgField);
            var bg = CompositeOver(GetColor(palette, bgField), palette.PanelBackground);
            var ratio = ContrastRatio(fg, bg);

            Assert.True(ratio >= minimumRatio,
                $"{label}: contrast {ratio:F2}:1 is below the required {minimumRatio}:1");
        }

        private static Color GetColor(ThemePalette palette, string fieldName)
            => (Color)typeof(ThemePalette).GetField(fieldName).GetValue(palette);

        /// <summary>Alpha-composites a (possibly translucent) color over an opaque backdrop.</summary>
        private static Color CompositeOver(Color top, Color backdrop)
        {
            var a = top.A / 255.0;
            byte Mix(byte t, byte b) => (byte)Math.Round(a * t + (1 - a) * b);
            return Color.FromRgb(Mix(top.R, backdrop.R), Mix(top.G, backdrop.G), Mix(top.B, backdrop.B));
        }

        /// <summary>WCAG 2.x contrast ratio between two opaque colors.</summary>
        private static double ContrastRatio(Color c1, Color c2)
        {
            var l1 = RelativeLuminance(c1);
            var l2 = RelativeLuminance(c2);
            var brighter = Math.Max(l1, l2);
            var darker = Math.Min(l1, l2);
            return (brighter + 0.05) / (darker + 0.05);
        }

        private static double RelativeLuminance(Color c)
        {
            double Channel(byte v)
            {
                var s = v / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }
    }
}
