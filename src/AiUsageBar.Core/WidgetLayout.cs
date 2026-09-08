namespace AiUsageBar.Core;

public static class WidgetLayout
{
    private const int RefWidgetHeight = 44;
    private const int RefFontPx = 12;

    public static int Height(int thickness) => Math.Max(28, thickness - 4);

    public static int IconSize(int height) => Math.Max(16, Math.Min(24, height - 10));

    public static int FontPx(int height)
    {
        var fromHeight = (int)Math.Round(height * (double)RefFontPx / RefWidgetHeight);
        return Math.Max(11, Math.Min(height - 16, fromHeight));
    }

    public static int Width(IReadOnlyList<int> textWidths, int icon)
    {
        if (textWidths.Count == 0)
        {
            return 120;
        }

        const int padX = 8;
        const int gap = 10;
        var width = padX;
        for (var index = 0; index < textWidths.Count; index++)
        {
            if (index > 0)
            {
                width += gap;
            }

            width += icon + 4 + textWidths[index];
        }

        width += padX;
        return Math.Min(560, Math.Max(width, 88));
    }
}
