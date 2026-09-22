using AIConnect4.App.ViewModels;

namespace AIConnect4.Tests;

public sealed class DecisionTimeFormatTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(0.4, "0.40 s")]
    [InlineData(2.34, "2.34 s")]
    [InlineData(9.99, "9.99 s")]
    [InlineData(10, "10.0 s")]
    [InlineData(45.2, "45.2 s")]
    [InlineData(59.9, "59.9 s")]
    public void Under_one_minute_keeps_fractional_seconds(double seconds, string expected) =>
        Assert.Equal(expected, DecisionTimeFormat.Format(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(60, "1m 0s")]
    [InlineData(61, "1m 1s")]
    [InlineData(154, "2m 34s")]
    [InlineData(3661, "61m 1s")]
    public void One_minute_and_above_uses_whole_minutes_and_seconds(double seconds, string expected) =>
        Assert.Equal(expected, DecisionTimeFormat.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Multi_minute_form_floors_fractional_seconds() =>
        Assert.Equal("2m 34s", DecisionTimeFormat.Format(TimeSpan.FromSeconds(154.9)));
}
