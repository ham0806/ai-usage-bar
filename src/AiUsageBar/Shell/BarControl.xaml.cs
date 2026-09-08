using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AiUsageBar.Core;

namespace AiUsageBar.Shell;

public partial class BarControl : UserControl
{
    private static readonly FontFamily BarFont = new("Segoe UI Variable Small, Segoe UI Variable Text, Segoe UI, Yu Gothic UI");

    public BarControl()
    {
        InitializeComponent();
    }

    public void Render(
        IReadOnlyList<ProviderSnapshot> snapshots,
        Theme theme,
        bool hover,
        string? barBg,
        int heightPx,
        int iconPx,
        int fontPx,
        double dipScale)
    {
        var fill = barBg ?? theme.Bg;
        var fontSize = fontPx * dipScale;
        Background = BrushFrom(fill);
        HoverBorder.Background = BrushFrom(hover ? Themes.HoverFill(fill) : fill);
        HoverBorder.Padding = new Thickness(8 * dipScale, 0, 8 * dipScale, 0);
        ToolTip = snapshots.Count == 0
            ? "AI Usage Bar"
            : string.Join("  |  ", snapshots.Select(item => $"{item.Title} {Formatting.ShortMetric(item)}")) + "\nクリックで詳細";
        Row.Children.Clear();
        if (snapshots.Count == 0)
        {
            Row.Children.Add(MakeText("AI Usage Bar", theme.Fg, fontSize, FontWeights.Normal));
            return;
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            if (index > 0)
            {
                Row.Children.Add(new Border { Width = 10 * dipScale });
            }

            var snapshot = snapshots[index];
            var icon = ProviderIcons.Load(snapshot.ProviderId, theme.Light, iconPx);
            if (icon is not null)
            {
                Row.Children.Add(new Image
                {
                    Source = icon,
                    Width = iconPx * dipScale,
                    Height = iconPx * dipScale,
                    Margin = new Thickness(0, 0, 4 * dipScale, 0),
                    Stretch = Stretch.Uniform,
                });
            }

            var color = snapshot.Ok ? Themes.PercentColor(theme, snapshot.UsedPercent) : theme.Warn;
            if (Formatting.IsUnlimited(snapshot))
            {
                color = theme.Ok;
            }

            Row.Children.Add(MakeText(Formatting.ShortMetric(snapshot), color, fontSize, FontWeights.Normal));
        }
    }

    public static int MeasureTextPx(string text, int fontPx, int dpi)
    {
        var dipScale = 96.0 / dpi;
        var fontSize = fontPx * dipScale;
        var pixelsPerDip = dpi / 96.0;
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(BarFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            Brushes.White,
            pixelsPerDip);
        return (int)Math.Ceiling(ft.WidthIncludingTrailingWhitespace * dpi / 96.0);
    }

    private static TextBlock MakeText(string text, string color, double fontSize, FontWeight weight) =>
        new()
        {
            Text = text,
            Foreground = BrushFrom(color),
            FontSize = fontSize,
            FontFamily = BarFont,
            FontWeight = weight,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
