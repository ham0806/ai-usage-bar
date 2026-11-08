using AiUsageBar.Core;
using AiUsageBar.Core.Win32;

namespace AiUsageBar.Tests;

public class WidgetLayoutTests
{
    [Fact]
    public void HeightFollowsTaskbarThickness()
    {
        Assert.Equal(44, WidgetLayout.Height(48));
        Assert.Equal(68, WidgetLayout.Height(72));
        Assert.Equal(28, WidgetLayout.Height(20));
    }

    [Fact]
    public void FontFollowsTaskbarHeight()
    {
        Assert.Equal(12, WidgetLayout.FontPx(44));
        Assert.Equal(19, WidgetLayout.FontPx(68));
        Assert.True(WidgetLayout.FontPx(44) <= 44 - 16);
    }

    [Fact]
    public void IconStaysInsideTheBar()
    {
        Assert.Equal(24, WidgetLayout.IconSize(44));
        Assert.Equal(18, WidgetLayout.IconSize(28));
    }

    [Fact]
    public void WidthIncludesIconsAndGaps()
    {
        Assert.Equal(120, WidgetLayout.Width([], 20));
        Assert.Equal(88, WidgetLayout.Width([28], 20));
        Assert.Equal(142, WidgetLayout.Width([28, 40], 20));
        Assert.Equal(88, WidgetLayout.Width([28], [0]));
        Assert.Equal(118, WidgetLayout.Width([28, 40], [20, 0]));
    }

    [Fact]
    public void HorizontalTaskbarFixesStaleLeftEdge()
    {
        // Windows 11 の Shell は uEdge=0 を返すことがある。矩形が横長なら下辺として扱う。
        Assert.Equal(3, Taskbar.NormalizeEdge(0, (0, 1392, 2560, 1440)));
        Assert.Equal(3, Taskbar.NormalizeEdge(2, (0, 1392, 2560, 1440)));
        Assert.Equal(1, Taskbar.NormalizeEdge(1, (0, 0, 2560, 48)));
    }

    [Fact]
    public void VerticalTaskbarKeepsLeftOrRightEdge()
    {
        Assert.Equal(0, Taskbar.NormalizeEdge(0, (0, 0, 60, 1440)));
        Assert.Equal(2, Taskbar.NormalizeEdge(2, (2500, 0, 2560, 1440)));
        Assert.Equal(0, Taskbar.NormalizeEdge(3, (0, 0, 60, 1440)));
    }

    [Fact]
    public void WidgetPositionSitsLeftOfTrayOnBottomTaskbar()
    {
        var layout = new TaskbarLayout(
            3,
            (0, 1040, 1920, 1080),
            (1700, 1040, 1920, 1080),
            0);
        var (x, y) = Taskbar.WidgetPosition(layout, 120, 36, gap: 6);
        Assert.Equal(1574, x);
        Assert.Equal(1042, y);
    }
}
