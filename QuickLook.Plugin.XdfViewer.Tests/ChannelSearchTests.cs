using QuickLook.Plugin.XdfViewer.ViewModels;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure predicate behind the channel-search box: case-insensitive substring match on the label,
    /// with an empty/whitespace query matching everything. No WPF.
    /// </summary>
    public class ChannelSearchTests
    {
        [Fact]
        public void Matches_EmptyQuery_MatchesEverything()
        {
            Assert.True(ChannelSearch.Matches("Fp1", ""));
        }

        [Fact]
        public void Matches_WhitespaceQuery_MatchesEverything()
        {
            Assert.True(ChannelSearch.Matches("Fp1", "   "));
        }

        [Fact]
        public void Matches_NullQuery_MatchesEverything()
        {
            Assert.True(ChannelSearch.Matches("Fp1", null));
        }

        [Fact]
        public void Matches_Substring_CaseInsensitive()
        {
            Assert.True(ChannelSearch.Matches("Fp1", "fp"));
            Assert.True(ChannelSearch.Matches("Fp1", "FP1"));
            Assert.True(ChannelSearch.Matches("accX", "X"));
        }

        [Fact]
        public void Matches_NonSubstring_False()
        {
            Assert.False(ChannelSearch.Matches("Fp1", "cz"));
        }

        [Fact]
        public void Matches_TrimsQueryWhitespace()
        {
            Assert.True(ChannelSearch.Matches("Fp1", "  fp  "));
        }

        [Fact]
        public void Matches_NullLabel_MatchesOnlyEmptyQuery()
        {
            Assert.True(ChannelSearch.Matches(null, ""));
            Assert.False(ChannelSearch.Matches(null, "fp"));
        }
    }
}
