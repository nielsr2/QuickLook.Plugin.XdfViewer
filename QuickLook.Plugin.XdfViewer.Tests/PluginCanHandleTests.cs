using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class PluginCanHandleTests
    {
        [Theory]
        [InlineData(@"C:\rec\a.xdf", true)]
        [InlineData(@"C:\rec\a.XDF", true)]
        [InlineData(@"C:\rec\a.xdfz", false)]   // compressed variant deferred
        [InlineData(@"C:\rec\a.h5", false)]
        [InlineData(@"C:\rec\a.txt", false)]
        public void CanHandle_MatchesXdfExtension(string path, bool expected)
        {
            Assert.Equal(expected, new Plugin().CanHandle(path));
        }
    }
}
