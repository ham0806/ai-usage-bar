using System.Text.Json;

namespace AiUsageBar.Core.Providers;

/// <summary>
/// 使用率ウィンドウ（5h / 7d など）を持つプロバイダで共通の読み取り。
/// </summary>
internal static class WindowReader
{
    public static double? UsedPercent(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var direct = Formatting.NumberOrString(obj, "used_percent", "usedPercent", "utilization", "percent", "percentage");
        if (direct is not null)
        {
            return Math.Clamp(direct.Value, 0, 100);
        }

        var used = Formatting.NumberOrString(obj, "used", "usage", "consumed");
        var cap = Formatting.NumberOrString(obj, "cap", "limit", "total", "quota");
        if (used is not null && cap is > 0)
        {
            return Math.Clamp(used.Value / cap.Value * 100.0, 0, 100);
        }

        return null;
    }

    public static DateTimeOffset? ResetsAt(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in new[] { "resets_at", "resetsAt", "reset_at", "resetAt", "reset" })
        {
            if (obj.TryGetProperty(key, out var value) && Formatting.ParseTimestamp(value) is { } parsed)
            {
                return parsed;
            }
        }

        return null;
    }

    public static RateWindow? Read(JsonElement obj, string label, int? durationMins = null)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var used = UsedPercent(obj);
        var resets = ResetsAt(obj);
        if (used is null && resets is null)
        {
            return null;
        }

        return new RateWindow(label, used, resets, durationMins);
    }

    /// <summary>
    /// ウィンドウを格納している辞書（本体と、よくある入れ子）を列挙する。
    /// </summary>
    public static IEnumerable<JsonElement> Containers(JsonElement root, params string[] nestedKeys)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return root;
        foreach (var key in nestedKeys)
        {
            if (Formatting.Object(root, key) is { } nested)
            {
                yield return nested;
            }
        }
    }
}
