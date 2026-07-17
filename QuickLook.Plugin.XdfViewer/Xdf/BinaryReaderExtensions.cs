using System.IO;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal static class BinaryReaderExtensions
    {
        /// <summary>
        /// Reads an XDF variable-length integer: one NumLengthBytes byte (1, 4 or 8)
        /// followed by that many little-endian bytes. BinaryReader is always little-endian.
        /// </summary>
        public static long ReadVarLength(this BinaryReader r)
        {
            byte n = r.ReadByte();
            switch (n)
            {
                case 1: return r.ReadByte();
                case 4: return r.ReadUInt32();
                case 8: return (long)r.ReadUInt64();
                default:
                    throw new InvalidDataException($"Invalid XDF NumLengthBytes: {n}");
            }
        }
    }
}
