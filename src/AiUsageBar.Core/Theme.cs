using Microsoft.Win32;

namespace AiUsageBar.Core;

public sealed record Theme(
    string Bg,
    string Fg,
    string Muted,
    string Accent,
    string Warn,
    string Bad,
    string Ok,
    string Border,
    string FlyoutBg,
    string Hover,
    bool Light);

public static class Themes
{
    public static readonly Theme Dark = new(
        "#202020",
        "#ffffff",
        "#c5c5c5",
        "#60cdff",
        "#f8d64e",
        "#ff5c7a",
        "#81d66c",
        "#3a3a3a",
        "#2c2c2c",
        "#5c5c5c",
        false);

    public static readonly Theme Light = new(
        "#f3f3f3",
        "#000000",
        "#5c5c5c",
        "#005fb8",
        "#9d5d00",
        "#c42b1c",
        "#0f7b0f",
        "#e5e5e5",
        "#f9f9f9",
        "#e8e8e8",
        true);

    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            if (value is int number)
            {
                return number != 0;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    public static string SystemAccent(bool light)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            var value = key?.GetValue("AccentColor");
            if (value is int raw)
            {
                var red = raw & 0xFF;
                var green = (raw >> 8) & 0xFF;
                var blue = (raw >> 16) & 0xFF;
                if (red != 0 || green != 0 || blue != 0)
                {
                    return $"#{red:x2}{green:x2}{blue:x2}";
                }
            }
        }
        catch (Exception)
        {
        }

        return light ? "#005fb8" : "#60cdff";
    }

    public static Theme Current()
    {
        var light = SystemUsesLightTheme();
        var baseTheme = light ? Light : Dark;
        return baseTheme with { Accent = SystemAccent(light) };
    }

    public static string PercentColor(Theme theme, double? usedPercent)
    {
        if (usedPercent is null)
        {
            return theme.Muted;
        }

        if (usedPercent >= 90)
        {
            return theme.Bad;
        }

        if (usedPercent >= 70)
        {
            return theme.Warn;
        }

        return theme.Ok;
    }

    public static (int R, int G, int B) ParseHex(string color)
    {
        var text = color.TrimStart('#');
        return (Convert.ToInt32(text[..2], 16), Convert.ToInt32(text[2..4], 16), Convert.ToInt32(text[4..6], 16));
    }

    public static string HexRgb(int red, int green, int blue) =>
        $"#{Math.Clamp(red, 0, 255):x2}{Math.Clamp(green, 0, 255):x2}{Math.Clamp(blue, 0, 255):x2}";

    public static string MixHex(string color, string toward, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var (r1, g1, b1) = ParseHex(color);
        var (r2, g2, b2) = ParseHex(toward);
        return HexRgb(
            (int)Math.Round(r1 + (r2 - r1) * amount),
            (int)Math.Round(g1 + (g2 - g1) * amount),
            (int)Math.Round(b1 + (b2 - b1) * amount));
    }

    public static bool HexClose(string left, string right, int tolerance = 3)
    {
        var (r1, g1, b1) = ParseHex(left);
        var (r2, g2, b2) = ParseHex(right);
        return Math.Max(Math.Abs(r1 - r2), Math.Max(Math.Abs(g1 - g2), Math.Abs(b1 - b2))) <= tolerance;
    }

    public static string HoverFill(string background)
    {
        var (red, green, blue) = ParseHex(background);
        var luma = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        var toward = luma > 140 ? "#000000" : "#ffffff";
        return MixHex(background, toward, 0.16);
    }

    public static uint HexToColorRef(string color)
    {
        var (r, g, b) = ParseHex(color);
        return (uint)(r | (g << 8) | (b << 16));
    }
}
