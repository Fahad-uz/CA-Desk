using CaDesk.Desktop;
using Xunit;
namespace CaDesk.Tests;

public class WindowPlacementTests
{
    [Theory]
    [InlineData(1366, 728, 1.0, 1334, 696)]
    [InlineData(1366, 728, 1.25, 1326, 688)]
    [InlineData(1366, 728, 1.5, 1318, 680)]
    [InlineData(1366, 728, 2.0, 1302, 664)]
    [InlineData(1920, 1040, 1.5, 1872, 992)]
    [InlineData(1920, 1040, 2.0, 1856, 976)]
    [InlineData(3840, 2112, 2.0, 2840, 1800)]
    public void StartupFitsUsablePixelsAtCommonDisplayScales(int width, int height, double scale, int expectedWidth, int expectedHeight)
    {
        var work = new PixelBounds(0, 0, width, height);
        var result = WindowPlacement.Fit(new(-100, -100, (int)(1420 * scale), (int)(900 * scale)), work, scale, true);
        Assert.Equal(expectedWidth, result.Width); Assert.Equal(expectedHeight, result.Height);
        Assert.True(result.Left >= work.Left && result.Top >= work.Top && result.Right <= work.Right && result.Bottom <= work.Bottom);
        Assert.InRange(Math.Abs((width - result.Width) / 2.0 - result.Left), 0, 1);
    }
    [Theory]
    [InlineData(1.0)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2.0)]
    public void MinimumSizeCannotForceCaptionOffSmallDisplay(double scale)
    {
        var work = new PixelBounds(0, 0, 1280, 680);
        var min = WindowPlacement.MinimumSize(work, scale);
        Assert.True(min.Width * scale < work.Width && min.Height * scale < work.Height);
        Assert.InRange(min.Width, 1, 1000); Assert.InRange(min.Height, 1, 680);
    }
    [Fact]
    public void SecondaryMonitorCanHaveNegativeCoordinatesAndTopTaskbar()
    {
        var work = new PixelBounds(-1920, 40, 1920, 1000);
        Assert.Equal(new PixelBounds(-1896, 64, 1872, 952), WindowPlacement.Fit(new(0, -80, 2130, 1350), work, 1.5, true));
    }
    [Fact]
    public void DisplayChangeKeepsExistingPositionWhenItAlreadyFits()
    {
        var original = new PixelBounds(300, 200, 1100, 700);
        Assert.Equal(original, WindowPlacement.Fit(original, new(0, 0, 1920, 1040), 1.25, false));
        var result = WindowPlacement.Fit(new(1600, -30, 1100, 700), new(0, 40, 1280, 640), 1.25, false);
        Assert.True(result.Left >= 0 && result.Top >= 40 && result.Right <= 1280 && result.Bottom <= 680);
    }
    [Fact]
    public void MaximizedNativeFrameInsetsAreConvertedToLogicalUnits()
    {
        var work = new PixelBounds(-1920, 40, 1920, 1000);
        Assert.Equal((8.0, 8.0, 8.0, 8.0), WindowPlacement.Insets(new(-1932, 28, 1944, 1024), work, 1.5));
        Assert.Equal((0.0, 0.0, 0.0, 0.0), WindowPlacement.Insets(work, work, 2));
    }
}
