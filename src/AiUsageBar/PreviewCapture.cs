using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AiUsageBar.Core;
using AiUsageBar.Core.Providers;
using AiUsageBar.Shell;

namespace AiUsageBar;

internal static class PreviewCapture
{
    public static void Run()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "output", "preview");
        Directory.CreateDirectory(dir);
        var snapshots = SampleSnapshots();
        foreach (var theme in new[] { Themes.Dark, Themes.Light })
        {
            var suffix = theme.Light ? "light" : "dark";
            SavePng(BuildBar(snapshots, theme), Path.Combine(dir, $"bar-{suffix}.png"));
            SavePng(BuildFlyout(snapshots, theme), Path.Combine(dir, $"flyout-{suffix}.png"));
        }
    }

    private static IReadOnlyList<ProviderSnapshot> SampleSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        using var cursor = JsonDocument.Parse("""
            {
              "membershipType": "pro",
              "individualUsage": {
                "plan": { "autoPercentUsed": 42.0, "apiPercentUsed": 18.5, "totalPercentUsed": 40 }
              },
              "billingCycleStart": "2026-08-08T00:00:00Z",
              "billingCycleEnd": "2026-09-08T00:00:00Z"
            }
            """);
        using var codex = JsonDocument.Parse($$"""
            {
              "plan_type": "plus",
              "rate_limit": {
                "primary_window": {
                  "used_percent": 18.2,
                  "limit_window_seconds": 18000,
                  "reset_at": {{now.AddHours(3).ToUnixTimeSeconds()}}
                },
                "secondary_window": {
                  "used_percent": 9,
                  "limit_window_seconds": 604800,
                  "reset_at": {{now.AddDays(4).ToUnixTimeSeconds()}}
                }
              }
            }
            """);
        return
        [
            CursorProvider.FromPayload(cursor.RootElement, now),
            CodexProvider.FromPayload(codex.RootElement, now),
        ];
    }

    private static FrameworkElement BuildBar(IReadOnlyList<ProviderSnapshot> snapshots, Theme theme)
    {
        const int height = 44;
        const int icon = 20;
        const int fontPx = 15;
        var textWidths = snapshots.Select(item => BarControl.MeasureTextPx(Formatting.ShortMetric(item), fontPx, 96)).ToList();
        var width = WidgetLayout.Width(textWidths, icon);
        var bar = new BarControl
        {
            Width = width,
            Height = height,
        };
        bar.Render(snapshots, theme, false, theme.Bg, height, icon, fontPx, 1);
        var host = new Border
        {
            Width = width + 24,
            Height = height + 24,
            Background = Brush(theme.Light ? "#dcdcdc" : "#101010"),
            Padding = new Thickness(12),
            Child = bar,
        };
        Prepare(host, host.Width, host.Height);
        return host;
    }

    private static FrameworkElement BuildFlyout(IReadOnlyList<ProviderSnapshot> snapshots, Theme theme)
    {
        var host = FlyoutWindow.CreateSurface(snapshots, theme);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Max(host.DesiredSize.Width, 304);
        var height = host.DesiredSize.Height;
        host.Width = width;
        host.Height = height;
        Prepare(host, width, height);
        return host;
    }

    private static void Prepare(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static void SavePng(FrameworkElement element, string path)
    {
        var width = Math.Max(1, (int)Math.Ceiling(element.RenderSize.Width));
        var height = Math.Max(1, (int)Math.Ceiling(element.RenderSize.Height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
