namespace QuickLook.Plugin.XdfViewer.Xdf.Model
{
    public sealed class StreamEnvelope
    {
        public uint StreamId { get; set; }
        public ChannelEnvelope[] Channels { get; set; }
        public bool IsApproximate { get; set; }
    }
}
