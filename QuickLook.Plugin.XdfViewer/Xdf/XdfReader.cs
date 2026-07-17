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
                    if (len < 2 || contentEnd > stream.Length)
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
                        // Samples/StreamFooter/ClockOffset/Boundary: handled in later tasks or skipped.
                    }

                    stream.Seek(contentEnd, SeekOrigin.Begin); // always resync to chunk boundary
                }
            }
            catch (EndOfStreamException) { file.IsTruncated = true; }
            catch (Exception) { file.IsTruncated = true; }

            foreach (var id in order)
                file.Streams.Add(streams[id]);
            return file;
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
