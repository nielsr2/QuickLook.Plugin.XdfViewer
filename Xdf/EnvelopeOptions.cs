namespace QuickLook.Plugin.XdfViewer.Xdf
{
    /// <summary>Options controlling <see cref="XdfReader.FillEnvelopes(Model.XdfFile, string, EnvelopeOptions)"/>.</summary>
    public sealed class EnvelopeOptions
    {
        /// <summary>Number of min/max buckets to build per channel.</summary>
        public int Buckets { get; set; } = XdfReader.SparklineBuckets;

        /// <summary>Soft cap on how many samples per stream get decoded (strided/decimated above this).</summary>
        public long MaxSamplesPerStream { get; set; } = XdfReader.MaxEnvelopeSamplesPerStream;

        /// <summary>When set, only this stream's envelope is computed.</summary>
        public uint? OnlyStreamId { get; set; } = null;

        /// <summary>
        /// When set, decode only a sub-range of the stream starting at this global sample index
        /// (clamped to the stream). Enables the full-view zoom to request just the visible window.
        /// Null (with <see cref="WindowSampleCount"/> null) means the whole stream.
        /// </summary>
        public long? StartSample { get; set; } = null;

        /// <summary>
        /// When set, the number of samples in the sub-range window (clamped to what remains after
        /// <see cref="StartSample"/>). Null means "to the end of the stream".
        /// </summary>
        public long? WindowSampleCount { get; set; } = null;
    }
}
