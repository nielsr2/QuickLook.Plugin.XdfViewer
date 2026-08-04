using QuickLook.Plugin.XdfViewer.ViewModels;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    /// <summary>
    /// Pure predicate behind the search box: case-insensitive substring match, used for both
    /// channel labels and marker values. An empty/whitespace query matches everything. No WPF.
    /// </summary>
    public class TextSearchTests
    {
        [Fact]
        public void Matches_EmptyQuery_MatchesEverything()
        {
            Assert.True(TextSearch.Matches("Fp1", ""));
        }

        [Fact]
        public void Matches_WhitespaceQuery_MatchesEverything()
        {
            Assert.True(TextSearch.Matches("Fp1", "   "));
        }

        [Fact]
        public void Matches_NullQuery_MatchesEverything()
        {
            Assert.True(TextSearch.Matches("Fp1", null));
        }

        [Fact]
        public void Matches_Substring_CaseInsensitive()
        {
            Assert.True(TextSearch.Matches("Fp1", "fp"));
            Assert.True(TextSearch.Matches("Fp1", "FP1"));
            Assert.True(TextSearch.Matches("accX", "X"));
        }

        [Fact]
        public void Matches_NonSubstring_False()
        {
            Assert.False(TextSearch.Matches("Fp1", "cz"));
        }

        [Fact]
        public void Matches_TrimsQueryWhitespace()
        {
            Assert.True(TextSearch.Matches("Fp1", "  fp  "));
        }

        [Fact]
        public void Matches_NullLabel_MatchesOnlyEmptyQuery()
        {
            Assert.True(TextSearch.Matches(null, ""));
            Assert.False(TextSearch.Matches(null, "fp"));
        }
    }
}
