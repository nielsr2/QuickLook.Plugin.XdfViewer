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
        private static readonly byte[] Magic = { 0x58, 0x44, 0x46, 0x3A }; // "XDF:"

        public static XdfFile Read(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                           1 << 16, FileOptions.SequentialScan))
                return Read(fs);
        }

        public static XdfFile Read(Stream stream)
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
                            ParseSamples(reader, contentEnd, streams, order);
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
                if (!s.SampleCount.HasValue)
                {
                    if (s.TalliedSampleCount > 0) s.SampleCount = s.TalliedSampleCount;
                    s.IsIncomplete = true; // no footer was seen
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
            var chunksByStream = new Dictionary<uint, List<EnvelopeChunkRef>>();
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

            // Target streams: numeric channel format, at least one channel, at least one indexed
            // sample, and matching the optional single-stream filter.
            foreach (var s in file.Streams)
            {
                if (opts.OnlyStreamId.HasValue && s.Id != opts.OnlyStreamId.Value) continue;
                if (s.ChannelCount <= 0) continue;
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

                        for (int c = 0; c < channelCount; c++)
                        {
                            double v = read(reader);
                            long g = cumBefore + i;
                            dec.Add(c, EnvelopeDecimator.BucketOf(g, total, opts.Buckets), v);
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

        private struct EnvelopeChunkRef
        {
            public long ValuesStart;
            public long ContentEnd;
            public long NumSamples;
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
        }

        private static void ParseStreamFooter(BinaryReader reader, long contentEnd,
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            var info = SafeRootInfo(ReadXml(reader, contentEnd));
            var s = GetOrAdd(streams, order, id);
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
            Dictionary<uint, XdfStream> streams, List<uint> order)
        {
            uint id = reader.ReadUInt32();
            long numSamples = reader.ReadVarLength();
            var s = GetOrAdd(streams, order, id);
            s.TalliedSampleCount += numSamples;

            if (!s.IsMarkerStream) return; // never decode numeric sample values

            var stream = reader.BaseStream;
            for (long i = 0; i < numSamples && stream.Position < contentEnd; i++)
            {
                byte tsMarker = reader.ReadByte();
                double ts = double.NaN;
                if (tsMarker == 8) ts = reader.ReadDouble();

                string value = null;
                int channelCount = s.ChannelCount > 0 ? s.ChannelCount : 1;
                for (int c = 0; c < channelCount; c++)
                {
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
