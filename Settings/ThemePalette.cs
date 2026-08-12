using System.Windows.Media;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.XdfViewer.Settings
{
    /// <summary>
    /// The light/dark color pair for every theme-dependent surface, as pure Color values —
    /// no WPF controls, so the whole palette (and the Themes→palette mapping) is unit-testable.
    /// XdfPreviewPanel.ApplyTheme turns these into frozen brushes / DynamicResource entries;
    /// the waveform accent system (PluginSettings.ResolveAccentColor) stays orthogonal and only
    /// ever overrides the brush derived from NeutralForeground.
    /// </summary>
    public sealed class ThemePalette
    {
        public readonly Color NeutralForeground;
        public readonly Color PanelBackground;
        public readonly Color OverlayChipBackground;
        public readonly Color OverlayChipForeground;
        public readonly Color SampleBadgeBackground;
        public readonly Color SampleBadgeForeground;
        public readonly Color MinimapBackground;
        public readonly Color MinimapViewportFill;
        public readonly Color MinimapViewportStroke;
        public readonly Color SecondaryForeground;
        public readonly Color MutedGlyphForeground;
        public readonly Color MarkerLabelBackground;
        public readonly Color MarkerLabelForeground;

        private ThemePalette(
            Color neutralForeground, Color panelBackground,
            Color overlayChipBackground, Color overlayChipForeground,
            Color sampleBadgeBackground, Color sampleBadgeForeground,
            Color minimapBackground, Color minimapViewportFill, Color minimapViewportStroke,
            Color secondaryForeground, Color mutedGlyphForeground,
            Color markerLabelBackground, Color markerLabelForeground)
        {
            NeutralForeground = neutralForeground;
            PanelBackground = panelBackground;
            OverlayChipBackground = overlayChipBackground;
            OverlayChipForeground = overlayChipForeground;
            SampleBadgeBackground = sampleBadgeBackground;
            SampleBadgeForeground = sampleBadgeForeground;
            MinimapBackground = minimapBackground;
            MinimapViewportFill = minimapViewportFill;
            MinimapViewportStroke = minimapViewportStroke;
            SecondaryForeground = secondaryForeground;
            MutedGlyphForeground = mutedGlyphForeground;
            MarkerLabelBackground = markerLabelBackground;
            MarkerLabelForeground = markerLabelForeground;
        }

        public static readonly ThemePalette Dark = new ThemePalette(
            neutralForeground: Color.FromRgb(0xEC, 0xEC, 0xEC),
            panelBackground: Color.FromRgb(0x1E, 0x1E, 0x1E),
            overlayChipBackground: Color.FromArgb(0xCC, 0x00, 0x00, 0x00),
            overlayChipForeground: Color.FromRgb(0xFF, 0xFF, 0xFF),
            sampleBadgeBackground: Color.FromArgb(0x66, 0x3F, 0xB9, 0x50),
            sampleBadgeForeground: Color.FromRgb(0xFF, 0xFF, 0xFF),
            minimapBackground: Color.FromArgb(0x40, 0x00, 0x00, 0x00),
            minimapViewportFill: Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF),
            minimapViewportStroke: Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF),
            secondaryForeground: Color.FromRgb(0x88, 0x88, 0x88),
            mutedGlyphForeground: Color.FromRgb(0x9A, 0x9A, 0x9A),
            markerLabelBackground: Color.FromArgb(0x99, 0x00, 0x00, 0x00),
            markerLabelForeground: Color.FromRgb(0xE0, 0x8E, 0x2A));

        public static readonly ThemePalette Light = new ThemePalette(
            neutralForeground: Color.FromRgb(0x1A, 0x1A, 0x1A),
            panelBackground: Color.FromRgb(0xF5, 0xF5, 0xF5),
            overlayChipBackground: Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
            overlayChipForeground: Color.FromRgb(0x1A, 0x1A, 0x1A),
            sampleBadgeBackground: Color.FromArgb(0x4D, 0x3F, 0xB9, 0x50),
            sampleBadgeForeground: Color.FromRgb(0x0F, 0x53, 0x23),
            minimapBackground: Color.FromArgb(0x15, 0x00, 0x00, 0x00),
            minimapViewportFill: Color.FromArgb(0x26, 0x00, 0x00, 0x00),
            minimapViewportStroke: Color.FromArgb(0xAA, 0x33, 0x33, 0x33),
            secondaryForeground: Color.FromRgb(0x6A, 0x6A, 0x6A),
            mutedGlyphForeground: Color.FromRgb(0x76, 0x76, 0x76),
            markerLabelBackground: Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
            markerLabelForeground: Color.FromRgb(0x8F, 0x50, 0x00));

        /// <summary>
        /// The OS flag decides only when the host declines to state a theme (Themes.None) —
        /// which is this plugin's common real-world runtime state, since ContextObject.Theme
        /// defaults to None and not every host overrides it before View() runs.
        /// </summary>
        public static bool IsDark(Themes theme, bool osAppsUseDarkTheme)
            => theme == Themes.Dark || (theme == Themes.None && osAppsUseDarkTheme);

        public static ThemePalette Resolve(Themes theme, bool osAppsUseDarkTheme)
            => IsDark(theme, osAppsUseDarkTheme) ? Dark : Light;
    }
}
