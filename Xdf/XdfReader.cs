using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using QuickLook.Plugin.XdfViewer.Xdf.Model;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    public static class XdfReader
    {
        public const int MaxMarkersPerStream = 500;
        public const int SparklineBuckets = 100;
        public const int ExpandedBuckets = 500;
        public const long MaxEnvelopeSamplesPerStream = 20_000;
        public const long MaxEnvelopeSamplesPerStreamHiRes = 200_000;
        public const long MaxEnvelopeSamplesPerStreamFull = 5_000_000;
        public const int MaxFullBuckets = 8_000;
        /// <summary>Upper bound on a stream's channel count for envelope building. Real devices are far
        /// below this (Trigno 144, HD-EEG ~256); a larger value is treated as a corrupt header and the
        /// stream gets no waveform (its metadata still shows), avoiding a huge/OOM allocation.</summary>
        public const int MaxEnvelopeChannels = 4_096;
        private static readonly byte[] Magic = { 0x58, 0x44, 0x46, 0x3A }; // "XDF:"

        /// <param name="regularTextAsEvents">Show text streams with a nominal rate &gt; 0 as events, like
        /// irregular marker streams (see <see cref="XdfStream.RegularTextAsEvents"/>).</param>
        public static XdfFile Read(string path, bool regularTextAsEvents = true)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                           1 << 16, FileOptions.SequentialScan))
                return Read(fs, regularTextAsEvents);
        }

        /// <summary>See <see cref="Read(string, bool)"/>.</summary>
        public static XdfFile Read(Stream stream, bool regularTextAsEvents = true)
        {
            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (magic.Length < 4 || !magic.SequenceEqual(Magic))
                return null;

            var file = new XdfFile();
            var streams = new Dictionary<uint, XdfStream>();
            var order = new List<uint>();

            try
            {
                while (stream.Position < stream.Length)
                {
                    long len = reader.ReadVarLength();
                    long tagPos = stream.Position;
                    long contentEnd = tagPos + len;
                    if (len < 2 || contentEnd < tagPos || contentEnd > stream.Length)
                    {
                        file.IsTruncated = true;
                        break;
                    }
                    var tag = (XdfChunkTag)reader.ReadUInt16();

                    switch (tag)
                    {
                        case XdfChunkTag.FileHeader:
                            ParseFileHeader(ReadXml(reader, contentEnd), file);
                            break;
                        case XdfChunkTag.StreamHeader:
                            ParseStreamHeader(reader, contentEnd, streams, order);
                            break;
                        case XdfChunkTag.StreamFooter:
                            ParseStreamFooter(reader, contentEnd, streams, order);
                            break;
                        case XdfChunkTag.Samples:
                            ParseSamples(reader, contentEnd, streams, order, regularTextAsEvents);
                            break;
                        // ClockOffset/Boundary: handled in later tasks or skipped.
                    }

                    stream.Seek(contentEnd, SeekOrigin.Begin); // always resync to chunk boundary
                }
            }
            catch (Exception) { file.IsTruncated = true; }

            foreach (var id in order)
            {
                var s = streams[id];
                s.RegularTextAsEvents = regularTextAsEvents; // also streams that never had a Samples chunk
                if (!s.SampleCount.HasValue)
                {
                    if (s.TalliedSampleCount > 0) s.SampleCount = s.TalliedSampleCount;
                    s.IsIncomplete = !s.HasFooter; // a footer without <sample_count> is still a complete stream
                }
                file.Streams.Add(s);
            }
            return file;
        }

        /// <summary>
        /// Builds bounded, strided min/max envelopes (sparklines/waveform previews) for the numeric
        /// streams in <paramref name="file"/>, re-reading <paramref name="path"/> independently of
        /// <see cref="Read(string)"/>. Never throws; problem streams are simply omitted.
        /// </summary>
        public static StreamEnvelope[] FillEnvelopes(XdfFile file, string path, EnvelopeOptions opts)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                           1 << 16, FileOptions.SequentialScan))
                return FillEnvelopes(file, fs, opts);
        }

        /// <summary>See <see cref="FillEnvelopes(XdfFile, string, EnvelopeOptions)"/>.</summary>
        public static StreamEnvelope[] FillEnvelopes(XdfFile file, Stream stream, EnvelopeOptions opts)
        {
            var result = new List<StreamEnvelope>();
            if (file == null || stream == null) return result.ToArray();
            opts = opts ?? new EnvelopeOptions();

            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (magic.Length < 4 || !magic.SequenceEqual(Magic))
                return result.ToArray();

            // Pass 1 (index): walk chunks, decoding nothing, and record where each Samples chunk's
            // value payload lives so Pass 2 can seek straight back to only the chunks it needs.
            // Built once and memoized on the file, so later reads (expand/full view/every zoom step)
            // skip this whole-file walk. The offsets are valid for any read over the same content.
            Dictionary<uint, List<EnvelopeChunkRef>> chunksByStream;
            if (file.ChunkIndex != null)
            {
                chunksByStream = file.ChunkIndex.ByStream;
            }
            else
            {
                var index = new XdfChunkIndex();
                chunksByStream = index.ByStream;
                try
                {
                    while (stream.Position < stream.Length)
                    {
                        long len = reader.ReadVarLength();
                        long tagPos = stream.Position;
                        long contentEnd = tagPos + len;
                        if (len < 2 || contentEnd < tagPos || contentEnd > stream.Length)
                            break;
                        var tag = (XdfChunkTag)reader.ReadUInt16();

                        if (tag == XdfChunkTag.Samples)
                        {
                            uint streamId = reader.ReadUInt32();
                            long numSamples = reader.ReadVarLength();
                            long valuesStart = stream.Position;

                            if (!chunksByStream.TryGetValue(streamId, out var chunks))
                            {
                                chunks = new List<EnvelopeChunkRef>();
                                chunksByStream[streamId] = chunks;
                            }
                            chunks.Add(new EnvelopeChunkRef
                            {
                                ValuesStart = valuesStart,
                                ContentEnd = contentEnd,
                                NumSamples = numSamples
                            });
                        }

                        stream.Seek(contentEnd, SeekOrigin.Begin); // always resync to chunk boundary
                    }
                }
                catch (Exception) { /* stop indexing; decode whatever chunks were captured */ }
                file.ChunkIndex = index;
            }

            // Target streams: numeric channel format, at least one channel, at least one indexed
            // sample, and matching the optional single-stream filter.
            foreach (var s in file.Streams)
            {
                if (opts.OnlyStreamId.HasValue && s.Id != opts.OnlyStreamId.Value) continue;
                if (s.ChannelCount <= 0 || s.ChannelCount > MaxEnvelopeChannels) continue;
                if (!XdfSampleFormat.TryGetNumeric(s.ChannelFormat, out _, out var read)) continue;
                if (!chunksByStream.TryGetValue(s.Id, out var chunks) || chunks.Count == 0) continue;

                long total = 0;
                foreach (var c in chunks) total += c.NumSamples;
                if (total <= 0) continue;

                try
                {
                    result.Add(DecodeStreamEnvelope(reader, stream, s, chunks, total, read, opts));
                }
                catch (Exception)
                {
                    // Truncated/corrupt payload for this stream only: skip it, keep the rest.
                }
            }

            return result.ToArray();
        }

        private static StreamEnvelope DecodeStreamEnvelope(BinaryReader reader, Stream stream, XdfStream s,
            List<EnvelopeChunkRef> chunks, long total, Func<BinaryReader, double> read, EnvelopeOptions opts)
        {
            // A windowed request (full-view zoom) decodes only the chunks overlapping the visible
            // sample range; the whole-stream path (sparklines/detail) is left byte-for-byte as-is.
            if (opts.StartSample.HasValue || opts.WindowSampleCount.HasValue)
                return DecodeWindow(reader, stream, s, chunks, total, read, opts);

            long cap = opts.MaxSamplesPerStream > 0 ? opts.MaxSamplesPerStream : 1;
            int k = total <= cap ? 1 : (int)Math.Ceiling((double)total / cap);
            bool approximate = k > 1;
            int lastIndex = chunks.Count - 1;
            int channelCount = s.ChannelCount;

            var dec = new EnvelopeDecimator(channelCount, opts.Buckets);

            long cumBefore = 0;
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var chunk = chunks[ci];
                if (ci % k == 0 || ci == lastIndex)
                {
                    stream.Seek(chunk.ValuesStart, SeekOrigin.Begin);
                    for (long i = 0; i < chunk.NumSamples && stream.Position < chunk.ContentEnd; i++)
                    {
                        byte tsMarker = reader.ReadByte();
                        if (tsMarker == 8) reader.ReadDouble();

                        long g = cumBefore + i;
                        int bkt = EnvelopeDecimator.BucketOf(g, total, opts.Buckets);
                        for (int c = 0; c < channelCount; c++)
                        {
                            if (stream.Position >= chunk.ContentEnd) break; // don't bleed into the next chunk
                            double v = read(reader);
                            dec.Add(c, bkt, v);
                        }
                    }
                }
                cumBefore += chunk.NumSamples;
            }

            return new StreamEnvelope
            {
                StreamId = s.Id,
                Channels = dec.Build(approximate),
                IsApproximate = approximate
            };
        }

        /// <summary>
        /// Decodes only the sample window [<paramref name="opts"/>.StartSample, +WindowSampleCount)
        /// of a stream. Selects the chunks overlapping the window; within a straddling chunk it
        /// decodes sequentially from the chunk start (per-sample byte size varies, so mid-chunk
        /// seeking is unsafe), reading-and-discarding samples before the window and stopping once
        /// past it. Buckets span the window, so at &lt;=1 sample/bucket Min==Max is per-sample exact.
        /// </summary>
        private static StreamEnvelope DecodeWindow(BinaryReader reader, Stream stream, XdfStream s,
            List<EnvelopeChunkRef> chunks, long total, Func<BinaryReader, double> read, EnvelopeOptions opts)
        {
            int channelCount = s.ChannelCount;

            long winStart = Math.Max(0, Math.Min(opts.StartSample ?? 0, total));
            long avail = total - winStart;
            long winCount = opts.WindowSampleCount ?? avail;
            if (winCount < 0) winCount = 0;
            if (winCount > avail) winCount = avail;
            long winEnd = winStart + winCount;

            long cap = opts.MaxSamplesPerStream > 0 ? opts.MaxSamplesPerStream : 1;
            long k = winCount <= cap ? 1 : (long)Math.Ceiling((double)winCount / cap);
            bool approximate = k > 1;

            var dec = new EnvelopeDecimator(channelCount, opts.Buckets);

            long cumBefore = 0;
            for (int ci = 0; ci < chunks.Count && cumBefore < winEnd; ci++)
            {
                var chunk = chunks[ci];
                long chunkStart = cumBefore;
                long chunkEnd = cumBefore + chunk.NumSamples;    // exclusive
                cumBefore = chunkEnd;

                if (chunkEnd <= winStart) continue;              // entirely before the window
                if (chunkStart >= winEnd) break;                 // entirely after (chunks are ordered)

                stream.Seek(chunk.ValuesStart, SeekOrigin.Begin);
                for (long i = 0; i < chunk.NumSamples && stream.Position < chunk.ContentEnd; i++)
                {
                    long g = chunkStart + i;
                    if (g >= winEnd) break;                      // past the window: stop this chunk

                    byte tsMarker = reader.ReadByte();
                    if (tsMarker == 8) reader.ReadDouble();

                    bool inWindow = g >= winStart;
                    long local = g - winStart;
                    // Keep the window's first & last samples so the ends are always represented.
                    bool take = inWindow && (k == 1 || local % k == 0 || g == winStart || g == winEnd - 1);
                    int bucket = inWindow ? EnvelopeDecimator.BucketOf(local, winCount, opts.Buckets) : 0;

                    for (int c = 0; c < channelCount; c++)
                    {
                        if (stream.Position >= chunk.ContentEnd) break; // don't bleed into the next chunk
                        double v = read(reader);                 // always read to advance the stream
                        if (take) dec.Add(c, bucket, v);
                    }
                }
            }

            return new StreamEnvelope
            {
                StreamId = s.Id,
                Channels = dec.Build(approximate),
                IsApproximate = approximate
            };
        }

        private static string ReadXml(BinaryReader reader, long contentEnd)
        {
            int len = (int)(contentEnd - reader.BaseStream.Position);
            if (len <= 0) return string.Empty;
            var bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void ParseFileHeader(string xml, XdfFile file)
        {
            var info = SafeRootInfo(xml);
            if (info == null) return;
            file.Version = (string)info.Element("version");
            var dt = (string)info.Element("datetime");
            if (DateTimeOffset.TryParse(dt, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
                file.RecordedAt = parsed;
        }

        private static void ParseStreamHeader(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            var info = SafeRootInfo(ReadXml(reader, contentEnd));
            var s = GetOrAdd(streams, order, id);
            if (info == null) return;

            s.Name = (string)info.Element("name");
            s.Type = (string)info.Element("type");
            s.ChannelFormat = (string)info.Element("channel_format");
            s.ChannelCount = ParseInt(info.Element("channel_count"), 0);
            s.NominalSampleRate = ParseDouble(info.Element("nominal_srate"), 0);

            s.Channels.Clear(); // a repeated header for the same id replaces the channel list, never appends
            var channels = info.Element("desc")?.Element("channels");
            if (channels != null)
            {
                foreach (var ch in channels.Elements("channel"))
                    s.Channels.Add(new XdfChannel
                    {
                        Label = (string)ch.Element("label"),
                        Unit = (string)ch.Element("unit"),
                        Type = (string)ch.Element("type")
                    });
            }

            // channel_count is what the samples carry; the channel list is only descriptive. A missing
            // count falls back to the list, and a list longer than the count is cut so every row maps
            // to a decoded channel.
            if (s.ChannelCount <= 0) s.ChannelCount = s.Channels.Count;
            else if (s.Channels.Count > s.ChannelCount) s.Channels.RemoveRange(s.ChannelCount, s.Channels.Count - s.ChannelCount);

            // Some outlets (e.g. g.tec Unicorn) write an empty <desc /> or a partial channel list. The
            // samples still carry channel_count values, so pad up to it (capped like envelope building)
            // and number any channel that has no usable label — otherwise the card shows no rows at all.
            int declared = Math.Min(s.ChannelCount, MaxEnvelopeChannels);
            while (s.Channels.Count < declared)
                s.Channels.Add(new XdfChannel());
            for (int i = 0; i < s.Channels.Count; i++)
                if (string.IsNullOrWhiteSpace(s.Channels[i].Label))
                    s.Channels[i].Label = "ch " + (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static void ParseStreamFooter(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            var info = SafeRootInfo(ReadXml(reader, contentEnd));
            var s = GetOrAdd(streams, order, id);
            s.HasFooter = true;
            if (info == null) return;

            var first = info.Element("first_timestamp");
            var last = info.Element("last_timestamp");
            var count = info.Element("sample_count");
            var srate = info.Element("measured_srate");

            if (first != null) s.FirstTimestamp = ParseDouble(first, 0);
            if (last != null) s.LastTimestamp = ParseDouble(last, 0);
            if (count != null) s.SampleCount = (long)ParseDouble(count, 0);
            if (srate != null) s.MeasuredSampleRate = ParseDouble(srate, 0);
        }

        private static void ParseSamples(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order, bool regularTextAsEvents)
        {
            uint id = reader.ReadUInt32();
            long numSamples = reader.ReadVarLength();
            var s = GetOrAdd(streams, order, id);
            s.RegularTextAsEvents = regularTextAsEvents; // decides IsMarkerStream just below
            s.TalliedSampleCount += numSamples;

            if (!s.IsMarkerStream) return; // never decode numeric sample values

            var stream = reader.BaseStream;
            for (long i = 0; i < numSamples && stream.Position < contentEnd; i++)
            {
                byte tsMarker = reader.ReadByte();
                double ts = double.NaN;
                if (tsMarker == 8) ts = reader.ReadDouble();
                else if (s.NominalSampleRate > 0 && !double.IsNaN(s.LastMarkerTimestamp))
                    ts = s.LastMarkerTimestamp + 1.0 / s.NominalSampleRate;
                s.LastMarkerTimestamp = ts;

                string value = null;
                int channelCount = s.ChannelCount > 0 ? s.ChannelCount : 1;
                for (int c = 0; c < channelCount; c++)
                {
                    if (stream.Position >= contentEnd) break; // don't bleed into the next chunk
                    long slen = reader.ReadVarLength();
                    var strBytes = reader.ReadBytes((int)slen);
                    var str = Encoding.UTF8.GetString(strBytes);
                    value = c == 0 ? str : value + " | " + str;
                }

                if (s.Markers.Count < MaxMarkersPerStream)
                    s.Markers.Add(new XdfMarker { Timestamp = ts, Value = value });
                else { s.MarkersTruncated = true; break; }
            }
        }

        private static XdfStream GetOrAdd(Dictionary<uint, XdfStream> streams, List<uint> order, uint id)
        {
            if (!streams.TryGetValue(id, out var s))
            {
                s = new XdfStream { Id = id };
                streams[id] = s;
                order.Add(id);
            }
            return s;
        }

        private static XElement SafeRootInfo(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try { return XDocument.Parse(xml).Root; }
            catch { return null; }
        }

        private static int ParseInt(XElement e, int fallback)
            => e != null && int.TryParse(e.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static double ParseDouble(XElement e, double fallback)
            => e != null && double.TryParse(e.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
