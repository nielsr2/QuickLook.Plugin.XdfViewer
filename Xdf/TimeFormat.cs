using System;
using System.Globalization;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>
    /// Human-friendly time formatting shared by the overview and full view. Short spans read in
    /// seconds; longer ones roll up to minutes and hours so a multi-minute/-hour recording doesn't
    /// show a large raw seconds count. Pure/WPF-free.
    /// </summary>
    public static class TimeFormat
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Duration display: "42 s" / "12.3 s" / "5m 12s" / "1h 02m 05s".</summary>
        public static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "?";
            if (seconds < 60) return seconds.ToString("0.#", Ci) + " s";

            long total = (long)seconds;   // truncate; don't round a duration up
            long h = total / 3600;
            long m = (total % 3600) / 60;
            long s = total % 60;
            return h > 0
                ? string.Format(Ci, "{0}h {1:00}m {2:00}s", h, m, s)
                : string.Format(Ci, "{0}m {1:00}s", m, s);
        }

        /// <summary>Timestamp display (keeps sub-second detail below an hour): "12.34 s" /
        /// "1m 23.4s" / "1h 02m 03s". Used for event/marker times.</summary>
        public static string FormatClock(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "?";
            bool neg = seconds < 0;
            double a = Math.Abs(seconds);
            string sign = neg ? "-" : "";

            if (a < 60) return sign + a.ToString("0.00", Ci) + " s";
            if (a < 3600)
            {
                long m = (long)(a / 60);
                double rem = a - m * 60;
                return sign + string.Format(Ci, "{0}m {1:0.0}s", m, rem);
            }
            long total = (long)a;
            long hh = total / 3600;
            long mm = (total % 3600) / 60;
            long ss = total % 60;
            return sign + string.Format(Ci, "{0}h {1:00}m {2:00}s", hh, mm, ss);
        }
    }
}
