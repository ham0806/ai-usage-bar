using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AiUsageBar.Core;
using AiUsageBar.Core.Win32;

namespace AiUsageBar.Shell;

public partial class FlyoutWindow : Window
{
    private DateTime _openedAt = DateTime.MinValue;
    private readonly DispatcherTimer _hideTimer;
    private static readonly FontFamily FlyoutFont = new("Segoe UI Variable Text, Segoe UI, Yu Gothic UI");

    public FlyoutWindow()
    {
        InitializeComponent();
        ShowActivated = true;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideIfUnfocused();
        };
        Deactivated += (_, _) => _hideTimer.Start();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        };
        SourceInitialized += (_, _) => ApplyChrome();
    }

    public void Toggle(IReadOnlyList<ProviderSnapshot> snapshots, TaskbarWidget anchor)
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowSnapshots(snapshots, anchor);
    }

    public void FillBody(IReadOnlyList<ProviderSnapshot> snapshots, Theme? theme = null)
    {
        theme ??= Themes.Current();
        Background = Brush(theme.FlyoutBg);
        Body.Background = Brush(theme.FlyoutBg);
        Body.Children.Clear();
        Populate(Body, snapshots, theme);
    }

    internal static FrameworkElement CreateSurface(IReadOnlyList<ProviderSnapshot> snapshots, Theme theme)
    {
        var body = new StackPanel
        {
            Margin = new Thickness(16, 14, 16, 14),
            Background = Brush(theme.FlyoutBg),
        };
        Populate(body, snapshots, theme);
        return new Border
        {
            Background = Brush(theme.FlyoutBg),
            MinWidth = 304,
            Child = body,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
    }

    private static void Populate(Panel body, IReadOnlyList<ProviderSnapshot> snapshots, Theme theme)
    {
        if (snapshots.Count == 0)
        {
            body.Children.Add(Label(UiText.NoProviders, 12, theme.Muted));
            return;
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            if (index > 0)
            {
                body.Children.Add(new Border
                {
                    Height = 1,
                    Background = Brush(theme.Border),
                    Margin = new Thickness(0, 12, 0, 12),
                });
            }

            body.Children.Add(BuildProvider(snapshots[index], theme));
        }
    }

    public void ShowSnapshots(IReadOnlyList<ProviderSnapshot> snapshots, TaskbarWidget anchor)
    {
        FillBody(snapshots);
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));
        var width = Math.Max(DesiredSize.Width, 304);
        var height = DesiredSize.Height;
        var dpi = VisualTreeHelper.GetDpi(this);
        double DipX(int px) => px * 96.0 / dpi.PixelsPerInchX;
        double DipY(int px) => px * 96.0 / dpi.PixelsPerInchY;
        var x = 8.0;
        var y = 8.0;
        if (anchor.ScreenRect is { } rect)
        {
            x = DipX(rect.Right) - width;
            y = DipY(rect.Top) - height - 10;
            if (y < 8)
            {
                y = DipY(rect.Bottom) + 10;
            }

            var screenH = SystemParameters.VirtualScreenHeight;
            if (y + height > screenH - 8)
            {
                y = Math.Max(8, screenH - height - 8);
            }

            x = Math.Max(8, x);
        }

        Left = x;
        Top = y;
        _openedAt = DateTime.UtcNow;
        Show();
        ApplyChrome();
        Activate();
        Focus();
    }

    private static UIElement BuildProvider(ProviderSnapshot snapshot, Theme theme)
    {
        var block = new StackPanel();
        block.Children.Add(Header(snapshot, theme));

        var primary = PrimaryText(snapshot);
        if (primary is not null)
        {
            var color = snapshot.Ok
                ? (Formatting.IsUnlimited(snapshot) ? theme.Ok : Themes.PercentColor(theme, snapshot.UsedPercent))
                : theme.Warn;
            block.Children.Add(Label(primary, 22, color, FontWeights.Bold, new Thickness(0, 6, 0, 2)));
        }

        foreach (var metric in snapshot.MetricList)
        {
            block.Children.Add(MetricRow(metric, theme));
        }

        foreach (var line in DetailLines(snapshot))
        {
            block.Children.Add(Label(line, 11, theme.Muted, null, new Thickness(0, 4, 0, 0)));
        }

        return block;
    }

    private static UIElement Header(ProviderSnapshot snapshot, Theme theme)
    {
        var row = new DockPanel { LastChildFill = true };
        var icon = ProviderIcons.Load(snapshot.ProviderId, theme.Light, 20);
        if (icon is not null)
        {
            var image = new Image
            {
                Source = icon,
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 8, 0),
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(image, Dock.Left);
            row.Children.Add(image);
        }

        if (!string.IsNullOrEmpty(snapshot.Subtitle))
        {
            var subtitle = Label(snapshot.Subtitle, 11, theme.Muted);
            subtitle.VerticalAlignment = VerticalAlignment.Center;
            subtitle.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(subtitle, Dock.Right);
            row.Children.Add(subtitle);
        }

        var title = Label(snapshot.Title, 13, theme.Fg, FontWeights.SemiBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(title);
        return row;
    }

    private static UIElement MetricRow(UsageMetric metric, Theme theme)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var row = new DockPanel { LastChildFill = true };
        var value = metric.UsedPercent is null
            ? (metric.Hint ?? "--")
            : Formatting.Percent(metric.UsedPercent.Value);
        var valueColor = metric.UsedPercent is null ? theme.Muted : Themes.PercentColor(theme, metric.UsedPercent);
        var valueLabel = Label(value, 12, valueColor, FontWeights.SemiBold);
        DockPanel.SetDock(valueLabel, Dock.Right);
        row.Children.Add(valueLabel);
        row.Children.Add(Label(Formatting.WindowLabel(metric.Label), 12, theme.Fg));
        stack.Children.Add(row);

        if (metric.UsedPercent is not null)
        {
            stack.Children.Add(Meter(metric.UsedPercent.Value, Themes.PercentColor(theme, metric.UsedPercent), Themes.MixHex(theme.FlyoutBg, theme.Fg, 0.14)));
        }

        if (metric.UsedPercent is not null && !string.IsNullOrEmpty(metric.Hint))
        {
            stack.Children.Add(Label(metric.Hint, 11, theme.Muted, null, new Thickness(0, 3, 0, 0)));
        }

        return stack;
    }

    private static FrameworkElement Meter(double percent, string fill, string track)
    {
        percent = Math.Clamp(percent, 0, 100);
        var root = new Grid { Height = 4, Margin = new Thickness(0, 4, 0, 0) };
        root.Children.Add(new Border
        {
            Background = Brush(track),
            CornerRadius = new CornerRadius(2),
        });
        if (percent <= 0)
        {
            return root;
        }

        var fillGrid = new Grid();
        fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(percent, GridUnitType.Star) });
        fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, 100 - percent), GridUnitType.Star) });
        var fillBar = new Border
        {
            Background = Brush(fill),
            CornerRadius = new CornerRadius(2),
        };
        Grid.SetColumn(fillBar, 0);
        fillGrid.Children.Add(fillBar);
        root.Children.Add(fillGrid);
        return root;
    }

    private static string? PrimaryText(ProviderSnapshot snapshot)
    {
        if (!snapshot.Ok && !string.IsNullOrEmpty(snapshot.Error))
        {
            return UiText.ErrorLabel(snapshot.Error);
        }

        if (Formatting.IsUnlimited(snapshot))
        {
            return "∞";
        }

        return snapshot.UsedPercent is null ? null : Formatting.Percent(snapshot.UsedPercent.Value);
    }

    private static IEnumerable<string> DetailLines(ProviderSnapshot snapshot)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { snapshot.Title };
        foreach (var metric in snapshot.MetricList)
        {
            skip.Add(metric.Label);
            skip.Add(Formatting.WindowLabel(metric.Label));
        }

        foreach (var line in snapshot.Lines)
        {
            var text = line.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var token = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            if (skip.Contains(token) || skip.Any(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return text;
        }
    }

    private void ApplyChrome()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            WindowChrome.ApplyToolWindowStyle(hwnd, noActivate: false);
            var theme = Themes.Current();
            WindowChrome.ApplyWin11Surface(hwnd, dark: !theme.Light, rounded: true, mica: true, border: theme.Border);
        }
        catch (Exception)
        {
        }
    }

    private void HideIfUnfocused()
    {
        if (!IsVisible)
        {
            return;
        }

        if ((DateTime.UtcNow - _openedAt).TotalSeconds < 0.4)
        {
            return;
        }

        if (!IsActive)
        {
            Hide();
        }
    }

    private static TextBlock Label(string text, double size, string color, FontWeight? weight = null, Thickness? margin = null) =>
        new()
        {
            Text = text,
            FontSize = size,
            Foreground = Brush(color),
            FontFamily = FlyoutFont,
            FontWeight = weight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0),
        };

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
