using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuickLook.Plugin.XdfViewer.ViewModels;
using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Runs every .xdf under the folder named by the XDF_CORPUS_DIR environment variable (searched
    /// recursively) through the same read → overview → sparkline path the preview uses, and checks
    /// that what the card shows matches what the file carries. Skipped when the variable is unset,
    /// so CI (which has no recordings) is unaffected. Point it at real study folders before a
    /// release: header/data mismatches from new devices show up here instead of as an empty card.
    /// </summary>
    public class RealFileCorpusTests
    {
        [Fact]
        public void EveryCorpusFile_ShowsOneRowWithAWaveformPerDecodedChannel()
        {
            var dir = Environment.GetEnvironmentVariable("XDF_CORPUS_DIR");
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return; // skipped without a corpus

            var problems = new List<string>();
            var files = Directory.GetFiles(dir, "*.xdf", SearchOption.AllDirectories);

            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                var file = XdfReader.Read(path);
                if (file == null) { problems.Add($"{name}: not recognised as XDF"); continue; }

                var vm = new XdfOverviewViewModel(file, name);
                var envs = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
                {
                    Buckets = XdfReader.SparklineBuckets,
                    MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStream
                });
                vm.ApplyEnvelopes(envs);

                foreach (var stream in file.Streams)
                {
                    var card = vm.Streams.First(c => c.StreamId == stream.Id);
                    var label = $"{name} / stream {stream.Id} ({stream.Name})";

                    if (stream.IsMarkerStream)
                    {
                        if (stream.TalliedSampleCount > 0 && card.Markers.Count == 0)
                            problems.Add($"{label}: {stream.TalliedSampleCount} samples but no events shown");
                        continue;
                    }

                    var env = envs.FirstOrDefault(e => e.StreamId == stream.Id);
                    if (env == null) continue; // not numeric, no samples, or over the channel cap: nothing to plot

                    if (card.Channels.Count != env.Channels.Length)
                        problems.Add($"{label}: {card.Channels.Count} rows for {env.Channels.Length} decoded channels");

                    var blank = card.Channels.Where(c => string.IsNullOrWhiteSpace(c.Label)).Count();
                    if (blank > 0) problems.Add($"{label}: {blank} rows without a label");

                    var noWave = card.Channels.Where(c => !c.HasWaveform).Select(c => c.Label).ToList();
                    if (noWave.Count > 0)
                        problems.Add($"{label}: no waveform for {string.Join(", ", noWave.Take(5))}" +
                                     (noWave.Count > 5 ? $" (+{noWave.Count - 5} more)" : ""));
                }
            }

            Assert.True(problems.Count == 0,
                $"{problems.Count} problem(s) across {files.Length} file(s):\n" + string.Join("\n", problems));
        }
    }
}
