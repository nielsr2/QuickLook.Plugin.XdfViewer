using System;
using System.IO;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    internal static class XdfSampleFormat
    {
        public static bool TryGetNumeric(string format, out int width, out Func<BinaryReader, double> read)
        {
            switch (format?.Trim().ToLowerInvariant())
            {
                case "int8":     width = 1; read = r => r.ReadSByte();  return true;
                case "int16":    width = 2; read = r => r.ReadInt16();  return true;
                case "int32":    width = 4; read = r => r.ReadInt32();  return true;
                case "int64":    width = 8; read = r => r.ReadInt64();  return true;
                case "float32":  width = 4; read = r => r.ReadSingle(); return true;
                case "double64": width = 8; read = r => r.ReadDouble(); return true;
                default:         width = 0; read = null;               return false;
            }
        }
    }
}
