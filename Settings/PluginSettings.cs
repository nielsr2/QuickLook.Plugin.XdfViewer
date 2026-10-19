using System;
using System.Windows.Media;
using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.XdfViewer.Settings
{
    public static class PluginSettings
    {
        public const string Domain = "QuickLook.Plugin.XdfViewer";

        private const string DefaultCustomWaveformColorHex = "#2FB8C6";

        // Enum settings are stored as their string name: SettingHelper round-trips
        // primitives via Convert.ChangeType, which cannot parse enum names (it would
        // silently fall back to the default). Enum.TryParse on the stored name restores
        // them.

        public static WaveformColorTheme WaveformColorTheme
        {
            get => ParseEnum("WaveformColorTheme", WaveformColorTheme.Purple);
            set => SettingHelper.Set("WaveformColorTheme", value.ToString(), Domain);
        }

        /// <summary>Show text streams that declare a sample rate &gt; 0 as events, like irregular marker
        /// streams. On by default; turning it off shows them as plain (waveform-less) streams.</summary>
        public static bool RegularTextStreamsAsEvents
        {
            get => SettingHelper.Get("RegularTextStreamsAsEvents", true, Domain);
            set => SettingHelper.Set("RegularTextStreamsAsEvents", value, Domain);
        }

        public static Color CustomWaveformColor
        {
            get
            {
                var stored = SettingHelper.Get("CustomWaveformColor", DefaultCustomWaveformColorHex, Domain);

                try
                {
                    if (ColorConverter.ConvertFromString(stored) is Color color)
                        return color;
                }
                catch (Exception)
                {
                }

                return (Color)ColorConverter.ConvertFromString(DefaultCustomWaveformColorHex);
            }
            set => SettingHelper.Set("CustomWaveformColor", $"#{value.R:X2}{value.G:X2}{value.B:X2}", Domain);
        }

        public static Color? ResolveAccentColor(WaveformColorTheme theme)
        {
            switch (theme)
            {
                case WaveformColorTheme.Mono:
                    return null;
                case WaveformColorTheme.Ocean:
                    return Color.FromRgb(0x2F, 0xB8, 0xC6);
                case WaveformColorTheme.Sunset:
                    return Color.FromRgb(0xF2, 0xA3, 0x3C);
                case WaveformColorTheme.Forest:
                    return Color.FromRgb(0x59, 0xC9, 0x7A);
                case WaveformColorTheme.Berry:
                    return Color.FromRgb(0xC8, 0x68, 0xE0);
                case WaveformColorTheme.Purple:
                    return Color.FromRgb(0x8A, 0x5C, 0xF6);
                case WaveformColorTheme.Custom:
                    return CustomWaveformColor;
                default:
                    return null;
            }
        }

        private static TEnum ParseEnum<TEnum>(string key, TEnum failsafe) where TEnum : struct
        {
            var stored = SettingHelper.Get(key, failsafe.ToString(), Domain);
            return Enum.TryParse(stored, true, out TEnum value) ? value : failsafe;
        }
    }
}
