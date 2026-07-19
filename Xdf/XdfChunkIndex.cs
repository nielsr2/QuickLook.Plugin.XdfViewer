using System.Collections.Generic;

namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>Location + sample count of one Samples chunk's value payload (Pass-1 index entry).</summary>
    internal struct EnvelopeChunkRef
    {
        public long ValuesStart;   // absolute byte offset of the first sample's bytes
        public long ContentEnd;    // absolute byte offset just past the chunk content
        public long NumSamples;    // sample count declared by the chunk
    }

    /// <summary>
    /// The Pass-1 index of every Samples chunk, keyed by stream id, built once by
    /// <see cref="XdfReader.FillEnvelopes(Model.XdfFile, System.IO.Stream, EnvelopeOptions)"/> and
    /// memoized on the <see cref="Model.XdfFile"/> so later envelope reads (each expand, the full
    /// view, and every zoom/pan step) reuse it instead of re-walking the whole file. The byte
    /// offsets are valid for any stream over the same file content.
    /// </summary>
    internal sealed class XdfChunkIndex
    {
        public readonly Dictionary<uint, List<EnvelopeChunkRef>> ByStream
            = new Dictionary<uint, List<EnvelopeChunkRef>>();
    }
}
