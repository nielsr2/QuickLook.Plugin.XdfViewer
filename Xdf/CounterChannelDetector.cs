using System;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>
    /// Recognizes a sample-counter channel — a value that climbs with the sample index rather than
    /// carrying signal, like OpenSignals' "nSeq". Worth flagging because such a channel sits in the
    /// list looking like data while being the one channel in a stream nobody wants to plot.
    /// <para>
    /// Counters that wrap are still counters, so a climb that drops back to its floor and resumes is
    /// accepted: the drop has to be most of the channel's range (a wrap returns to the bottom, where a
    /// signal only wiggles), and every run between drops has to be its own straight climb.
    /// </para>
    /// <para>
    /// Works off the sparkline envelope alone — no raw samples are ever decoded for the overview — so
    /// it only sees what per-bucket min/max preserves. A counter that wraps faster than a bucket is
    /// therefore out of reach: every bucket ends up spanning the counter's whole range, which is
    /// indistinguishable from a full-scale signal.
    /// </para>
    /// Pure/WPF-free.
    /// </summary>
    public static class CounterChannelDetector
    {
        /// <summary>Below this many decoded buckets there isn't enough of a trend to call it.</summary>
        internal const int MinFilledBuckets = 8;
        /// <summary>Shortest run between wraps that still counts as a climb rather than a coincidence.</summary>
        internal const int MinRunBuckets = 4;
        /// <summary>More wraps than this and the "resets" are better explained as signal.</summary>
        internal const int MaxWraps = 8;
        /// <summary>Slack for float32 rounding, as a fraction of the range: a 1.5M sample index is held
        /// to about 0.1, so neighbouring buckets can overlap slightly without going backwards.</summary>
        internal const double RelativeTolerance = 1e-3;
        /// <summary>How far a drop must fall, as a fraction of the range, to read as a wrap instead of
        /// a signal's downswing.</summary>
        internal const double WrapDropFraction = 0.5;
        /// <summary>How far a bucket may sit off its run's straight line, as a fraction of what that run
        /// spans. Keeps slow curves — physiological drift — from reading as counters.</summary>
        internal const double LinearityTolerance = 0.12;

        public static bool IsSampleCounter(ChannelEnvelope e)
        {
            if (e == null || e.IsEmpty || e.Min == null || e.Max == null || e.Filled == null) return false;

            double range = e.DataMax - e.DataMin;
            if (range <= 0) return false;         // constant channel — IsFlat's territory, not a counter

            double tolerance = range * RelativeTolerance;
            double minWrapDrop = range * WrapDropFraction;
            int n = Math.Min(e.BucketCount, e.Filled.Length);

            // Walk the decoded buckets, cutting a new run wherever the value wraps. Unfilled buckets are
            // skipped throughout: chunk striding leaves most of them empty on a long stream, and the
            // ones that survive still lie on the counter's line.
            int runStart = -1, previous = -1, filled = 0, wraps = 0;

            for (int b = 0; b < n; b++)
            {
                if (!e.Filled[b]) continue;
                filled++;

                if (previous < 0) { runStart = b; previous = b; continue; }

                if (e.Min[b] >= e.Max[previous] - tolerance)   // still climbing
                {
                    previous = b;
                    continue;
                }

                // It fell. Only a wrap — back down to the floor — is allowed; anything shallower is a
                // signal changing direction, which settles it immediately.
                if (e.Max[previous] - e.Max[b] < minWrapDrop) return false;
                if (++wraps > MaxWraps) return false;
                if (!IsStraightClimb(e, runStart, previous)) return false;

                runStart = b;
                previous = b;
            }

            if (filled < MinFilledBuckets || previous < 0) return false;
            return IsStraightClimb(e, runStart, previous);
        }

        /// <summary>True when every decoded bucket in [first, last] sits on the straight line between
        /// the two ends, and that line climbs — a counter advances by a fixed step per sample, so its
        /// run is a chord, where drift bows away from one.</summary>
        private static bool IsStraightClimb(ChannelEnvelope e, int first, int last)
        {
            int span = last - first;
            if (span <= 0) return false;

            int buckets = 0;
            for (int b = first; b <= last; b++) if (e.Filled[b]) buckets++;
            if (buckets < MinRunBuckets) return false;

            double firstMid = Mid(e, first), lastMid = Mid(e, last);
            double climb = lastMid - firstMid;
            if (climb <= 0) return false;

            double slope = climb / span;
            double maxDeviation = climb * LinearityTolerance;

            for (int b = first; b <= last; b++)
            {
                if (!e.Filled[b]) continue;
                double expected = firstMid + slope * (b - first);
                if (Math.Abs(Mid(e, b) - expected) > maxDeviation) return false;
            }

            return true;
        }

        private static double Mid(ChannelEnvelope e, int bucket) => (e.Min[bucket] + e.Max[bucket]) * 0.5;
    }
}
