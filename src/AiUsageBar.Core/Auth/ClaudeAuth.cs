using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiUsageBar.Core.Auth;

public sealed record ClaudeCredentials(string? AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt);

public sealed record ClaudeRefreshResult(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt);

public static class ClaudeAuth
{
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    public const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";

    public static string CredentialsPath => Path.Combine(ConfigDir, ".credentials.json");

    public static string ConfigDir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir.Trim();
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude");
        }
    }

    public static ClaudeCredentials? Load(string? path = null)
    {
        path ??= CredentialsPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("Claude の .credentials.json を読めませんでした");
            return null;
        }
    }

    public static ClaudeCredentials? Parse(string json) => Parse(ParseRoot(json));

    private static ClaudeCredentials? Parse(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        var oauth = root["claudeAiOauth"] as JsonObject ?? root;
        var access = Text(oauth, "accessToken", "access_token");
        var refresh = Text(oauth, "refreshToken", "refresh_token");
        var expires = oauth["expiresAt"] ?? oauth["expires_at"];
        if (access is null && refresh is null)
        {
            return null;
        }

        return new ClaudeCredentials(access, refresh, ExpiresAt(expires));
    }

    public static bool NeedsRefresh(ClaudeCredentials credentials, DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(credentials.RefreshToken))
        {
            return false;
        }

        if (credentials.ExpiresAt is null)
        {
            return false;
        }

        now ??= Formatting.UtcNow();
        return credentials.ExpiresAt.Value <= now.Value.AddMinutes(2);
    }

    public static string RefreshRequestJson(string refreshToken) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
        });

    public static async Task<ClaudeRefreshResult?> RefreshAsync(string refreshToken, HttpClient http, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent(RefreshRequestJson(refreshToken), Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 400)
            {
                AppLog.Warning($"Claude のトークン更新が拒否されました status={(int)response.StatusCode}");
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var access = Formatting.PickStr(doc.RootElement, "access_token");
            if (string.IsNullOrEmpty(access))
            {
                return null;
            }

            var expiresIn = Formatting.Number(doc.RootElement, "expires_in");
            return new ClaudeRefreshResult(
                access,
                Formatting.PickStr(doc.RootElement, "refresh_token"),
                expiresIn is null ? null : Formatting.UtcNow().AddSeconds(expiresIn.Value));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            AppLog.Warning("Claude のトークン更新リクエストに失敗しました");
            return null;
        }
    }

    public static void ApplyRefreshed(ClaudeRefreshResult tokens, string? path = null)
    {
        path ??= CredentialsPath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var root = ParseRoot(File.ReadAllText(path)) ?? [];
            var oauth = root["claudeAiOauth"] as JsonObject ?? [];
            oauth["accessToken"] = tokens.AccessToken;
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                oauth["refreshToken"] = tokens.RefreshToken;
            }

            if (tokens.ExpiresAt is not null)
            {
                oauth["expiresAt"] = tokens.ExpiresAt.Value.ToUnixTimeMilliseconds();
            }

            root["claudeAiOauth"] = oauth;
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            AppLog.Info("Claude のトークンを更新して保存しました");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            AppLog.Warning("Claude の .credentials.json へ更新トークンを書けませんでした");
        }
    }

    private static JsonObject? ParseRoot(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            AppLog.Warning("Claude の .credentials.json を解釈できませんでした");
            return null;
        }
    }

    private static string? Text(JsonObject? obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj?[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static DateTimeOffset? ExpiresAt(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<long>(out var ms))
        {
            return ms > 1_000_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : DateTimeOffset.FromUnixTimeSeconds(ms);
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number > 1e12 ? DateTimeOffset.FromUnixTimeMilliseconds((long)number) : DateTimeOffset.FromUnixTimeSeconds((long)number);
        }

        if (value.TryGetValue<string>(out var text))
        {
            return Formatting.ParseTimestamp(text);
        }

        return null;
    }
}
