using System.Text.Json;

namespace AiUsageBar.Core;

public sealed record RateWindow(
    string Label,
    double? UsedPercent,
    DateTimeOffset? ResetsAt = null,
    int? DurationMins = null)
{
    public double? RemainingPercent =>
        UsedPercent is null ? null : Math.Clamp(100.0 - UsedPercent.Value, 0, 100);

    public string Compact() => RemainingPercent is null
        ? $"{Label} --"
        : $"{Label} {Formatting.Percent(RemainingPercent.Value)}";
}

public sealed record UsageMetric(
    string Label,
    double? UsedPercent = null,
    string? Hint = null);

public sealed record ProviderSnapshot(
    string ProviderId,
    string Title,
    bool Ok,
    string Compact,
    IReadOnlyList<string> Lines,
    string? Error = null,
    DateTimeOffset? UpdatedAt = null,
    double? UsedPercent = null,
    string? Subtitle = null,
    IReadOnlyList<UsageMetric>? Metrics = null)
{
    public IReadOnlyList<UsageMetric> MetricList => Metrics ?? [];
}

public static class Formatting
{
    public static string Percent(double value)
    {
        if (value < 0)
        {
            value = 0;
        }

        if (value > 999)
        {
            value = 999;
        }

        return Math.Abs(value - Math.Round(value)) < 0.05
            ? $"{Math.Round(value):0}%"
            : $"{value:0.0}%";
    }

    public static DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    public static DateTimeOffset? ParseTimestamp(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var integer) => FromUnix(integer),
            JsonValueKind.Number when value.TryGetDouble(out var number) => FromUnix(number),
            JsonValueKind.String => ParseTimestamp(value.GetString()),
            _ => null,
        };
    }

    public static DateTimeOffset? ParseTimestamp(object? value)
    {
        switch (value)
        {
            case null:
            case "":
                return null;
            case DateTimeOffset dto:
                return dto.ToUniversalTime();
            case DateTime dt:
                return dt.Kind == DateTimeKind.Unspecified
                    ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
                    : new DateTimeOffset(dt.ToUniversalTime());
            case int i:
                return FromUnix(i);
            case long l:
                return FromUnix(l);
            case double d:
                return FromUnix(d);
            case string text:
                return ParseTimestamp(text);
            case JsonElement element:
                return ParseTimestamp(element);
            default:
                return null;
        }
    }

    public static DateTimeOffset? ParseTimestamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        if (text.All(char.IsDigit))
        {
            return long.TryParse(text, out var unix) ? FromUnix(unix) : null;
        }

        if (DateTimeOffset.TryParse(text.Replace("Z", "+00:00"), out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    public static string FormatReset(DateTimeOffset? resetsAt, DateTimeOffset? now = null)
    {
        if (resetsAt is null)
        {
            return "不明";
        }

        now ??= UtcNow();
        var seconds = (int)(resetsAt.Value - now.Value).TotalSeconds;
        if (seconds <= 0)
        {
            return "まもなく";
        }

        var minutes = seconds / 60;
        var hours = minutes / 60;
        minutes %= 60;
        var days = hours / 24;
        hours %= 24;
        if (days > 0)
        {
            return $"{days}日{hours}時間後";
        }

        if (hours > 0)
        {
            return $"{hours}時間{minutes}分後";
        }

        return $"{minutes}分後";
    }

    public static Dictionary<string, JsonElement> JwtPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return [];
        }

        var payload = parts[1];
        var pad = (4 - payload.Length % 4) % 4;
        try
        {
            var raw = Convert.FromBase64String((payload + new string('=', pad)).Replace('-', '+').Replace('_', '/'));
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.Clone().EnumerateObject())
            {
                map[prop.Name] = prop.Value.Clone();
            }

            return map;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return [];
        }
    }

    public static string ComposeCompact(IEnumerable<ProviderSnapshot> snapshots)
    {
        var parts = snapshots.Select(item => item.Compact).Where(text => !string.IsNullOrEmpty(text)).ToList();
        return parts.Count == 0 ? "AI Usage Bar" : string.Join("  |  ", parts);
    }

    public static string ShortMetric(ProviderSnapshot snapshot)
    {
        if (!snapshot.Ok && !string.IsNullOrEmpty(snapshot.Error))
        {
            return snapshot.Error!;
        }

        if (IsUnlimited(snapshot))
        {
            return "∞";
        }

        if (snapshot.UsedPercent is not null)
        {
            return Percent(snapshot.UsedPercent.Value);
        }

        var text = StripTitle(snapshot);
        return string.IsNullOrEmpty(text) ? "--" : text;
    }

    public static bool IsUnlimited(ProviderSnapshot snapshot) =>
        snapshot.Ok && snapshot.Compact.Contains('∞');

    public static string WindowLabel(string label) => label switch
    {
        "5h" => "5時間",
        "7d" => "7日",
        "1d" => "1日",
        "30d" => "30日",
        "1y" => "1年",
        _ => label,
    };

    public static string StripTitle(ProviderSnapshot snapshot)
    {
        var text = snapshot.Compact;
        foreach (var prefix in new[] { $"{snapshot.Title} ", "Cursor ", "Codex " })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..];
            }
        }

        return text;
    }

    public static string? PickStr(JsonElement obj, params string[] keys)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    public static string? PickStr(IReadOnlyDictionary<string, JsonElement> obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    public static double? Number(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return null;
    }

    public static double? Number(JsonElement obj, string key)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value)
            ? Number(value)
            : null;
    }

    public static JsonElement? Object(JsonElement parent, string key)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return null;
    }

    private static DateTimeOffset? FromUnix(double ts)
    {
        if (ts > 1e12)
        {
            ts /= 1000.0;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(ts * 1000.0));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
