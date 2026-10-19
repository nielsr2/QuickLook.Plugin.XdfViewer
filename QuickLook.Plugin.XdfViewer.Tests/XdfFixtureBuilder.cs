using System.Globalization;
using System.IO;
using System.Text;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>Writes spec-correct XDF bytes for tests. Uses 4-byte chunk lengths.</summary>
    public sealed class XdfFixtureBuilder
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly BinaryWriter _bw;

        public XdfFixtureBuilder(bool withMagic = true)
        {
            _bw = new BinaryWriter(_ms, Encoding.UTF8, leaveOpen: true);
            if (withMagic) _bw.Write(new byte[] { 0x58, 0x44, 0x46, 0x3A }); // "XDF:"
        }

        public void Chunk(ushort tag, byte[] content)
        {
            _bw.Write((byte)4);                          // NumLengthBytes
            _bw.Write((uint)(content.Length + 2));       // Length = tag(2) + content
            _bw.Write(tag);                              // Tag (LE)
            _bw.Write(content);
        }

        private static void WriteVarLen(BinaryWriter w, long value)
        {
            w.Write((byte)4);
            w.Write((uint)value);
        }

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

        public XdfFixtureBuilder FileHeader(string version = "1.0", string datetime = null)
        {
            var dt = datetime == null ? "" : $"<datetime>{datetime}</datetime>";
            Chunk(1, Utf8($"<?xml version=\"1.0\"?><info><version>{version}</version>{dt}</info>"));
            return this;
        }

        public XdfFixtureBuilder StreamHeaderXml(uint id, string xml)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                w.Write(Utf8(xml));
                Chunk(2, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder StreamHeader(uint id, string name, string type,
            int channelCount, double srate, string format, params (string label, string unit)[] channels)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?><info>");
            sb.Append($"<name>{name}</name><type>{type}</type>");
            sb.Append($"<channel_count>{channelCount}</channel_count>");
            sb.Append($"<nominal_srate>{srate.ToString(CultureInfo.InvariantCulture)}</nominal_srate>");
            sb.Append($"<channel_format>{format}</channel_format>");
            if (channels != null && channels.Length > 0)
            {
                sb.Append("<desc><channels>");
                foreach (var c in channels)
                    sb.Append($"<channel><label>{c.label}</label><unit>{c.unit}</unit></channel>");
                sb.Append("</channels></desc>");
            }
            sb.Append("</info>");
            return StreamHeaderXml(id, sb.ToString());
        }

        public XdfFixtureBuilder SamplesRaw(uint id, long numSamples, int fillerBytes = 0)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, numSamples);
                if (fillerBytes > 0) w.Write(new byte[fillerBytes]);
                Chunk(3, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder SamplesNumeric(uint id, string format, int channelCount,
            bool withTimestamps, params double[][] samples)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, samples.Length);
                foreach (var sample in samples)
                {
                    if (withTimestamps) { w.Write((byte)8); w.Write(0.0); } else w.Write((byte)0);
                    for (int c = 0; c < channelCount; c++) WriteNumericValue(w, format, sample[c]);
                }
                Chunk(3, m.ToArray());
            }
            return this;
        }

        private static void WriteNumericValue(BinaryWriter w, string format, double v)
        {
            switch (format)
            {
                case "int8":     w.Write((sbyte)v);  break;
                case "int16":    w.Write((short)v);  break;
                case "int32":    w.Write((int)v);    break;
                case "int64":    w.Write((long)v);   break;
                case "float32":  w.Write((float)v);  break;
                case "double64": w.Write(v);         break;
                default: throw new System.ArgumentException($"unsupported format {format}");
            }
        }

        public XdfFixtureBuilder SamplesString(uint id, params (double ts, string val)[] markers)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                WriteVarLen(w, markers.Length);
                foreach (var mk in markers)
                {
                    w.Write((byte)8);           // timestamp present
                    w.Write(mk.ts);             // double LE
                    var val = Utf8(mk.val);
                    WriteVarLen(w, val.Length);  // string value length
                    w.Write(val);
                }
                Chunk(3, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder StreamFooter(uint id, double first, double last, long count)
        {
            var ci = CultureInfo.InvariantCulture;
            var xml = "<?xml version=\"1.0\"?><info>" +
                      $"<first_timestamp>{first.ToString(ci)}</first_timestamp>" +
                      $"<last_timestamp>{last.ToString(ci)}</last_timestamp>" +
                      $"<sample_count>{count}</sample_count></info>";
            return StreamFooterXml(id, xml);
        }

        public XdfFixtureBuilder StreamFooterXml(uint id, string xml)
        {
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(id);
                w.Write(Utf8(xml));
                Chunk(6, m.ToArray());
            }
            return this;
        }

        public XdfFixtureBuilder Raw(byte[] bytes) { _bw.Write(bytes); return this; }

        public byte[] ToBytes() { _bw.Flush(); return _ms.ToArray(); }
        public MemoryStream ToStream() => new MemoryStream(ToBytes());
    }
}
