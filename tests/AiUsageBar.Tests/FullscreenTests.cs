using AiUsageBar.Core.Win32;

namespace AiUsageBar.Tests;

public class FullscreenTests
{
    private const int WsCaption = 0x00C00000;
    private const int WsMaximize = 0x01000000;
    private const int WsMinimize = 0x20000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private static readonly (int, int, int, int) Monitor = (0, 0, 1920, 1080);
    private static readonly (int, int, int, int) WorkArea = (0, 0, 1920, 1040);

    [Fact]
    public void PopupCoveringMonitorIsFullscreen()
    {
        var style = WsPopup | WsVisible;
        Assert.True(Fullscreen.IsFullscreenCover(Monitor, Monitor, style));
    }

    [Fact]
    public void NearCoverWithinToleranceIsFullscreen()
    {
        var style = WsPopup | WsVisible;
        Assert.True(Fullscreen.IsFullscreenCover((-4, -2, 1922, 1081), Monitor, style));
    }

    [Fact]
    public void WorkAreaWindowIsNotFullscreen()
    {
        var style = WsPopup | WsVisible;
        Assert.False(Fullscreen.IsFullscreenCover(WorkArea, Monitor, style));
    }

    [Fact]
    public void MaximizedCaptionedWindowIsNotFullscreen()
    {
        var style = WsCaption | WsMaximize | WsVisible;
        Assert.False(Fullscreen.IsFullscreenCover(Monitor, Monitor, style));
    }

    [Fact]
    public void MinimizedWindowIsNotFullscreen()
    {
        var style = WsPopup | WsMinimize | WsVisible;
        Assert.False(Fullscreen.IsFullscreenCover(Monitor, Monitor, style));
    }

    [Fact]
    public void ShellClassesAreIgnored()
    {
        Assert.True(Fullscreen.IsShellWindowClass("Progman"));
        Assert.True(Fullscreen.IsShellWindowClass("Shell_TrayWnd"));
        Assert.True(Fullscreen.IsShellWindowClass("XamlExplorerHostIslandWindow"));
        Assert.True(Fullscreen.IsShellWindowClass("MultitaskingViewFrame"));
        Assert.False(Fullscreen.IsShellWindowClass("Chrome_WidgetWin_1"));
        Assert.False(Fullscreen.IsShellWindowClass("Windows.UI.Core.CoreWindow"));
    }

    [Fact]
    public void StoreAppOrStartMenuDoesNotHide()
    {
        Assert.False(Fullscreen.NotificationStateForcesYield(Fullscreen.QunsApp));
    }

    [Fact]
    public void BusyOrBrowserFullscreenDoesNotShortCircuit()
    {
        Assert.False(Fullscreen.NotificationStateForcesYield(Fullscreen.QunsBusy));
    }

    [Fact]
    public void ExclusiveD3dAndPresentationStillHide()
    {
        Assert.True(Fullscreen.NotificationStateForcesYield(Fullscreen.QunsRunningD3dFullScreen));
        Assert.True(Fullscreen.NotificationStateForcesYield(Fullscreen.QunsPresentationMode));
    }

    [Fact]
    public void UnknownStateDoesNotHide()
    {
        Assert.False(Fullscreen.NotificationStateForcesYield(null));
        Assert.False(Fullscreen.NotificationStateForcesYield(5));
    }

    [Fact]
    public void RestoreAfterFullscreenHide()
    {
        Assert.True(Fullscreen.NeedsWidgetRestore(false, false, true));
    }

    [Fact]
    public void RestoreIfWindowStaysHiddenAfterFlagClears()
    {
        Assert.True(Fullscreen.NeedsWidgetRestore(false, false, false));
    }

    [Fact]
    public void NoRestoreWhileYielding()
    {
        Assert.False(Fullscreen.NeedsWidgetRestore(true, false, true));
    }

    [Fact]
    public void NoRestoreWhenAlreadyVisible()
    {
        Assert.False(Fullscreen.NeedsWidgetRestore(false, true, false));
    }
}
