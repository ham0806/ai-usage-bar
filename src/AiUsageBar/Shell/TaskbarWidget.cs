using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AiUsageBar.Core;
using AiUsageBar.Core.Win32;

namespace AiUsageBar.Shell;

public sealed class TaskbarWidget : IDisposable
{
    public const int MenuRefresh = 1;
    public const int MenuSettings = 2;
    public const int MenuExit = 3;

    private readonly Func<(int OffsetX, int OffsetY)> _getOffset;
    private readonly Action _onLeftClick;
    private readonly Action _onRefresh;
    private readonly Action _onSettings;
    private readonly Action _onExit;
    private readonly BarControl _control = new();
    private readonly DispatcherTimer _tick;
    private HwndSource? _source;
    private IReadOnlyList<ProviderSnapshot> _snapshots = [];
    private Theme _theme = Themes.Current();
    private bool _hover;
    private bool _hiddenForFullscreen;
    private bool _styled;
    private string _barBg;
    private int _pixelWidth = 120;
    private int _pixelHeight = 28;
    private int _dpi = 96;

    public TaskbarWidget(
        Func<(int OffsetX, int OffsetY)> getOffset,
        Action onLeftClick,
        Action onRefresh,
        Action onSettings,
        Action onExit)
    {
        _getOffset = getOffset;
        _onLeftClick = onLeftClick;
        _onRefresh = onRefresh;
        _onSettings = onSettings;
        _onExit = onExit;
        _barBg = _theme.Bg;
        _control.MouseEnter += (_, _) => SetHover(true);
        _control.MouseLeave += (_, _) => SetHover(false);
        _control.MouseLeftButtonUp += (_, e) =>
        {
            _onLeftClick();
            e.Handled = true;
        };
        _control.MouseRightButtonUp += OnRightClick;
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _tick.Tick += (_, _) => Tick();
    }

    public nint Hwnd => _source?.Handle ?? 0;

    public int PixelWidth => _pixelWidth;

    public int PixelHeight => _pixelHeight;

    public (int Left, int Top, int Right, int Bottom)? ScreenRect
    {
        get
        {
            var hwnd = Hwnd;
            if (hwnd == 0 || !WindowChrome.TryGetWindowRect(hwnd, out var rect))
            {
                return null;
            }

            return rect;
        }
    }

    public void Show()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("AI Usage Bar")
        {
            Width = _pixelWidth,
            Height = _pixelHeight,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x00000080 | 0x08000000 | 0x00000008,
            UsesPerPixelOpacity = false,
            PositionX = 0,
            PositionY = 0,
        };
        _source = new HwndSource(parameters)
        {
            RootVisual = _control,
            SizeToContent = SizeToContent.Manual,
        };
        _source.CompositionTarget!.BackgroundColor = ToColor(_barBg);
        try
        {
            WindowChrome.ApplyToolWindowStyle(Hwnd, noActivate: true);
            _styled = true;
        }
        catch (Exception)
        {
            _styled = false;
        }

        Redraw();
        Dock();
        _tick.Start();
    }

    public void SetSnapshots(IReadOnlyList<ProviderSnapshot> snapshots)
    {
        _snapshots = snapshots;
        if (!_hiddenForFullscreen)
        {
            Redraw();
        }

        Dock();
    }

    public void Dispose()
    {
        _tick.Stop();
        _source?.Dispose();
        _source = null;
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover)
        {
            return;
        }

        _hover = hover;
        Redraw();
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var hwnd = Hwnd;
        var point = _control.PointToScreen(e.GetPosition(_control));
        var command = NativeMenu.Show(
            hwnd,
            (int)point.X,
            (int)point.Y,
            [
                (MenuRefresh, "今すぐ更新(&R)"),
                (MenuSettings, "設定(&S)"),
                null,
                (MenuExit, "終了(&X)"),
            ]);
        if (command == MenuRefresh)
        {
            _onRefresh();
        }
        else if (command == MenuSettings)
        {
            _onSettings();
        }
        else if (command == MenuExit)
        {
            _onExit();
        }

        e.Handled = true;
    }

    private string ResolveBarBg(TaskbarLayout? layout)
    {
        var sampled = layout is null ? null : Taskbar.SampleColor(layout, ScreenRect);
        if (sampled is null)
        {
            return _theme.Bg;
        }

        return Themes.HexClose(_barBg, sampled) ? _barBg : sampled;
    }

    private void Redraw()
    {
        _theme = Themes.Current();
        var layout = Taskbar.GetLayout();
        _dpi = Taskbar.MonitorDpi(layout?.Taskbar, Hwnd);
        var dipScale = 96.0 / _dpi;
        _barBg = ResolveBarBg(layout);
        var thickness = layout is null ? 48 : Taskbar.Thickness(layout);
        var height = WidgetLayout.Height(thickness);
        var icon = WidgetLayout.IconSize(height);
        var fontPx = WidgetLayout.FontPx(height);
        int width;
        if (_snapshots.Count == 0)
        {
            width = WidgetLayout.Width([], icon);
        }
        else
        {
            var textWidths = _snapshots.Select(item => BarControl.MeasureTextPx(Formatting.ShortMetric(item), fontPx, _dpi)).ToList();
            width = WidgetLayout.Width(textWidths, icon);
        }

        _pixelWidth = width;
        _pixelHeight = height;
        _control.Width = width * dipScale;
        _control.Height = height * dipScale;
        _control.Render(_snapshots, _theme, _hover, _barBg, height, icon, fontPx, dipScale);
        if (_source?.CompositionTarget is { } target)
        {
            target.BackgroundColor = ToColor(_barBg);
        }
    }

    private static Color ToColor(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    private void HideForFullscreen()
    {
        if (_hiddenForFullscreen && !WindowChrome.IsWindowShown(Hwnd))
        {
            return;
        }

        if (!_hiddenForFullscreen)
        {
            AppLog.Info("全画面のためバーを隠します");
        }

        _hiddenForFullscreen = true;
        WindowChrome.SetWindowShown(Hwnd, false);
    }

    public void Dock()
    {
        var layout = Taskbar.GetLayout();
        var hwnd = Hwnd;
        var yielding = Fullscreen.ShouldYieldToFullscreen(layout, hwnd);
        var visible = WindowChrome.IsWindowShown(hwnd);
        if (yielding)
        {
            HideForFullscreen();
            return;
        }

        if (Fullscreen.NeedsWidgetRestore(false, visible, _hiddenForFullscreen))
        {
            if (_hiddenForFullscreen)
            {
                AppLog.Info("全画面が終わったのでバーを再表示します");
            }

            _hiddenForFullscreen = false;
            WindowChrome.SetWindowShown(hwnd, true);
            _styled = false;
            Redraw();
        }
        else
        {
            _hiddenForFullscreen = false;
        }

        if (layout is null || hwnd == 0)
        {
            return;
        }

        var (ox, oy) = _getOffset();
        var (x, y) = Taskbar.WidgetPosition(layout, _pixelWidth, _pixelHeight, ox, oy, gap: 6);
        if (!_styled)
        {
            try
            {
                WindowChrome.ApplyToolWindowStyle(hwnd, noActivate: true);
                _styled = true;
            }
            catch (Exception)
            {
            }
        }

        try
        {
            WindowChrome.PlaceTopmost(hwnd, x, y, _pixelWidth, _pixelHeight);
        }
        catch (Exception)
        {
        }
    }

    private void Tick()
    {
        try
        {
            var layout = Taskbar.GetLayout();
            if (Fullscreen.ShouldYieldToFullscreen(layout, Hwnd))
            {
                HideForFullscreen();
                return;
            }

            var theme = Themes.Current();
            var barBg = ResolveBarBg(layout);
            var dpi = Taskbar.MonitorDpi(layout?.Taskbar, Hwnd);
            var thickness = layout is null ? 48 : Taskbar.Thickness(layout);
            var sizeChanged = dpi != _dpi || WidgetLayout.Height(thickness) != _pixelHeight;
            if (sizeChanged || theme != _theme || barBg != _barBg)
            {
                Redraw();
            }

            Dock();
        }
        catch (Exception ex)
        {
            AppLog.Error("バーの定期更新でエラー", ex);
        }
    }
}
