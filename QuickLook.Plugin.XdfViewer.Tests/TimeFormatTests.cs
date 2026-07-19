using QuickLook.Plugin.XdfViewer.Xdf;
using Xunit;

namespace QuickLook.Plugin.XdfViewer.Tests
{
    public class TimeFormatTests
    {
        [Theory]
        [InlineData(0, "0 s")]
        [InlineData(12.3, "12.3 s")]
        [InlineData(42, "42 s")]
        [InlineData(59.9, "59.9 s")]
        [InlineData(90, "1m 30s")]
        [InlineData(312, "5m 12s")]
        [InlineData(3600, "1h 00m 00s")]
        [InlineData(3725, "1h 02m 05s")]
        public void FormatDuration_RollsUpToMinutesAndHours(double seconds, string expected)
        {
            Assert.Equal(expected, TimeFormat.FormatDuration(seconds));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void FormatDuration_InvalidReturnsQuestionMark(double seconds)
        {
            Assert.Equal("?", TimeFormat.FormatDuration(seconds));
        }

        [Theory]
        [InlineData(0, "0.00 s")]
        [InlineData(12.34, "12.34 s")]
        [InlineData(83.4, "1m 23.4s")]
        [InlineData(3723, "1h 02m 03s")]
        public void FormatClock_KeepsSubSecondBelowAnHour(double seconds, string expected)
        {
            Assert.Equal(expected, TimeFormat.FormatClock(seconds));
        }
    }
}
