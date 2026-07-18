namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class ChannelEnvelope
    {
        public float[] Min { get; set; }
        public float[] Max { get; set; }
        public bool[] Filled { get; set; }
        public int BucketCount { get; set; }
        public float DataMin { get; set; }
        public float DataMax { get; set; }
        public bool IsApproximate { get; set; }
        public bool IsEmpty { get; set; }
    }
}
