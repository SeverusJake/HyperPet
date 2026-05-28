using HyperPet.Core.Settings;
using Xunit;

namespace HyperPet.Core.Tests.Settings;

public class QuietHoursScheduleTests
{
    [Theory]
    [InlineData("22:00", 22, 0)]
    [InlineData("07:30", 7, 30)]
    [InlineData("  09:05  ", 9, 5)]
    [InlineData("00:00", 0, 0)]
    public void TryParse_ParsesValidTimes(string text, int hour, int minute)
    {
        var parsed = QuietHoursSchedule.TryParse(text);
        Assert.NotNull(parsed);
        Assert.Equal(new TimeOnly(hour, minute), parsed!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9:5")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("noon")]
    public void TryParse_RejectsInvalid(string? text)
    {
        Assert.Null(QuietHoursSchedule.TryParse(text));
    }

    [Fact]
    public void IsActive_SameWindow_DaytimeRange()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(9, 0), start, end));   // inclusive start
        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(12, 0), start, end));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(17, 0), start, end)); // exclusive end
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(8, 59), start, end));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(18, 0), start, end));
    }

    [Fact]
    public void IsActive_OvernightWrap()
    {
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(7, 0);

        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(23, 0), start, end));
        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(2, 0), start, end));
        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(22, 0), start, end));  // inclusive start
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(7, 0), start, end));  // exclusive end
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(12, 0), start, end));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(21, 59), start, end));
    }

    [Fact]
    public void IsActive_EqualStartEnd_IsAlwaysInactive()
    {
        var t = new TimeOnly(10, 0);
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(10, 0), t, t));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(3, 0), t, t));
    }

    [Fact]
    public void IsActive_StringOverload_FalseWhenUnparseable()
    {
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(23, 0), "garbage", "07:00"));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(23, 0), "22:00", null));
    }

    [Fact]
    public void IsActive_StringOverload_MatchesParsedWindow()
    {
        Assert.True(QuietHoursSchedule.IsActive(new TimeOnly(23, 0), "22:00", "07:00"));
        Assert.False(QuietHoursSchedule.IsActive(new TimeOnly(12, 0), "22:00", "07:00"));
    }
}
