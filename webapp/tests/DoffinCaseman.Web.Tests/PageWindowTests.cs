using DoffinCaseman.Web.Services;
using Xunit;

namespace DoffinCaseman.Web.Tests;

public class PageWindowTests
{
    // Renders the window as e.g. "1 ... 4 5 [6] 7 8 ... 20" for readable assertions.
    private static string Render(int current, int total) =>
        string.Join(' ', PageWindow.Build(current, total).Select(i =>
            i is null ? "..." : i == current ? $"[{i}]" : i.ToString()));

    [Theory]
    [InlineData(1, 1, "[1]")]
    [InlineData(1, 5, "[1] 2 3 4 5")]
    [InlineData(3, 7, "1 2 [3] 4 5 6 7")]
    [InlineData(1, 20, "[1] 2 3 ... 20")]
    [InlineData(4, 20, "1 2 3 [4] 5 6 ... 20")]
    [InlineData(10, 20, "1 ... 8 9 [10] 11 12 ... 20")]
    [InlineData(17, 20, "1 ... 15 16 [17] 18 19 20")]
    [InlineData(20, 20, "1 ... 18 19 [20]")]
    public void Build_ProducesExpectedWindow(int current, int total, string expected) =>
        Assert.Equal(expected, Render(current, total));

    [Fact]
    public void Build_FillsSinglePageGapInsteadOfEllipsis() =>
        // Page 2 is the only page hidden between 1 and 3; showing it beats "...".
        Assert.Equal("1 2 3 4 [5] 6 7 ... 20", Render(5, 20));

    [Theory]
    [InlineData(0, 10)]
    [InlineData(99, 10)]
    [InlineData(1, 0)]
    public void Build_ToleratesOutOfRangeInput(int current, int total)
    {
        var items = PageWindow.Build(current, total);

        Assert.NotEmpty(items);
        Assert.Equal(1, items[0]);
    }
}
