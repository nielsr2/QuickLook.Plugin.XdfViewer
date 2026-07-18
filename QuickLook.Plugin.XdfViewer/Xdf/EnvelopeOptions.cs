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
    }
}
