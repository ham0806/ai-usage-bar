using System.Text.Json;

namespace AiUsageBar.Core.Auth;

public static class CommandCodeAuth
{
    public const string BaseUrl = "https://api.commandcode.ai";
    public const string EnvVar = "COMMAND_CODE_API_KEY";

    public static string AuthJsonPath
    {
        get
        {
            var home = Environment.GetEnvironmentVariable("COMMANDCODE_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".commandcode");
            return Path.Combine(home, "auth.json");
        }
    }

    public static string? LoadToken(string? path = null)
    {
        path ??= AuthJsonPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return ReadToken(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("Command Code の auth.json を読めませんでした");
            return null;
        }
    }

    /// <summary>
    /// auth.json は版によって形が変わるので、既知のキーを優先しつつ入れ子も浅く探す。
    /// </summary>
    public static string? ReadToken(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ReadToken(doc.RootElement, depth: 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly string[] TokenKeys =
    [
        "token",
        "access_token",
        "accessToken",
        "apiKey",
        "api_key",
        "api_key_id",
        "key",
        "session",
        "sessionToken",
    ];

    private static string? ReadToken(JsonElement element, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object || depth > 3)
        {
            return null;
        }

        foreach (var key in TokenKeys)
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (IsToken(text))
                {
                    return text;
                }
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nested = ReadToken(property.Value, depth + 1);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool IsToken(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length >= 8 && !text.Contains(' ', StringComparison.Ordinal);
}
