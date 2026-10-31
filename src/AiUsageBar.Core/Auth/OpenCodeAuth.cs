using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiUsageBar.Core.Providers;

namespace AiUsageBar.Core.Auth;

public sealed record OpenCodeDeviceCode(string DeviceCode, string UserCode, string VerificationUrl, int ExpiresIn, int Interval);

/// <summary>
/// console のセッション。WorkspaceId は device 認証の org_id（wrk_ / org_）。
/// go/status は x-org-id でワークスペースを指定するため、分かれば保存しておく。
/// </summary>
public sealed record OpenCodeSession(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string? WorkspaceId = null);

public static class OpenCodeAuth
{
    public const string EnvVar = "OPENCODE_API_KEY";
    public const string ConsoleBase = "https://opencode.ai/console";
    public const string ClientId = "opencode-cli";

    private static readonly string[] ProviderIds = ["opencode-go", "opencode"];

    public static string DeviceCodeUrl => $"{ConsoleBase}/auth/device/code";

    public static string DeviceTokenUrl => $"{ConsoleBase}/auth/device/token";

    public static string DataDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg.Trim(), "opencode");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share",
                "opencode");
        }
    }

    public static string AuthJsonPath => Path.Combine(DataDir, "auth.json");

    /// <summary>
    /// console の device 認証を開始する。返した URL をブラウザで開いて承認してもらう。
    /// </summary>
    public static async Task<OpenCodeDeviceCode?> RequestDeviceCodeAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var payload = await PostAsync(
            http,
            DeviceCodeUrl,
            """{"client_id":"opencode-cli","supports_org_scope":true}""",
            cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        var deviceCode = Formatting.PickStr(payload.Value, "device_code");
        var userCode = Formatting.PickStr(payload.Value, "user_code");
        var complete = Formatting.PickStr(payload.Value, "verification_uri_complete");
        if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode))
        {
            return null;
        }

        var url = string.IsNullOrEmpty(complete) ? $"{ConsoleBase}/device" : complete;
        if (url.StartsWith('/'))
        {
            url = new Uri(new Uri(ConsoleBase), url).ToString();
        }

        return new OpenCodeDeviceCode(
            deviceCode,
            userCode,
            url,
            (int)(Formatting.Number(payload.Value, "expires_in") ?? 600),
            (int)(Formatting.Number(payload.Value, "interval") ?? 5));
    }

    /// <summary>
    /// 承認されるまで polling する。中断は cancellationToken で行う。
    /// </summary>
    public static async Task<OpenCodeSession?> PollDeviceTokenAsync(
        HttpClient http,
        OpenCodeDeviceCode code,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, code.ExpiresIn));
        var interval = Math.Max(2, code.Interval);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
            var body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = code.DeviceCode,
                ["client_id"] = ClientId,
            });
            var payload = await PostAsync(http, DeviceTokenUrl, body, cancellationToken, allowError: true).ConfigureAwait(false);
            if (payload is null)
            {
                continue;
            }

            var session = SessionFrom(payload.Value);
            if (session is not null)
            {
                return session;
            }

            var error = Formatting.PickStr(payload.Value, "error");
            if (error is "authorization_pending" or "slow_down")
            {
                if (error == "slow_down")
                {
                    interval += 5;
                }

                continue;
            }

            AppLog.Warning($"OpenCode の device 認証が失敗しました error={error ?? "unknown"}");
            return null;
        }

        AppLog.Warning("OpenCode の device 認証が時間切れになりました");
        return null;
    }

    public static async Task<OpenCodeSession?> RefreshAsync(HttpClient http, string refreshToken, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
        });
        var payload = await PostAsync(http, DeviceTokenUrl, body, cancellationToken).ConfigureAwait(false);
        return payload is null ? null : SessionFrom(payload.Value);
    }

    public static OpenCodeSession? LoadSession()
    {
        var raw = CredentialStore.GetToken(CredentialUsers.OpenCodeConsole);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return SessionFrom(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void SaveSession(OpenCodeSession? session)
    {
        if (session is null)
        {
            CredentialStore.SetToken(CredentialUsers.OpenCodeConsole, null);
            return;
        }

        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["access_token"] = session.AccessToken,
            ["refresh_token"] = session.RefreshToken,
            ["expires_at"] = session.ExpiresAt?.ToUnixTimeSeconds(),
            ["org_id"] = session.WorkspaceId,
        });
        CredentialStore.SetToken(CredentialUsers.OpenCodeConsole, json);
    }

    public static bool NeedsRefresh(OpenCodeSession session, DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(session.RefreshToken) || session.ExpiresAt is null)
        {
            return false;
        }

        now ??= Formatting.UtcNow();
        return session.ExpiresAt.Value <= now.Value.AddMinutes(2);
    }

    internal static OpenCodeSession? SessionFrom(JsonElement payload)
    {
        var access = Formatting.PickStr(payload, "access_token");
        if (string.IsNullOrEmpty(access))
        {
            return null;
        }

        var expiresIn = Formatting.NumberOrString(payload, "expires_in", "expiresIn");
        DateTimeOffset? expiresAt = expiresIn is null
            ? null
            : Formatting.UtcNow().AddSeconds(expiresIn.Value);
        if (expiresAt is null)
        {
            // SaveSession は expires_at（Unix 秒）で保存する。再起動後も期限を見て更新する。
            foreach (var key in new[] { "expires_at", "expiresAt" })
            {
                if (payload.TryGetProperty(key, out var value) && Formatting.ParseTimestamp(value) is { } parsed)
                {
                    expiresAt = parsed;
                    break;
                }
            }
        }

        var workspace = OpenCodeProvider.NormalizeWorkspaceId(
            Formatting.PickStr(payload, "org_id", "orgId", "workspace_id", "workspaceId"));
        return new OpenCodeSession(
            access,
            Formatting.PickStr(payload, "refresh_token"),
            expiresAt,
            workspace);
    }

    private static async Task<JsonElement?> PostAsync(
        HttpClient http,
        string url,
        string json,
        CancellationToken cancellationToken,
        bool allowError = false)
    {
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (status >= 400 && !allowError)
            {
                AppLog.Warning($"OpenCode の device 認証リクエストが失敗しました status={status}");
                return null;
            }

            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ex is not TaskCanceledException || !cancellationToken.IsCancellationRequested)
            {
                AppLog.Warning("OpenCode の device 認証リクエストに失敗しました");
            }

            return null;
        }
    }

    public static string? LoadApiKey(string? path = null)
    {
        path ??= AuthJsonPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return ReadApiKey(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("OpenCode の auth.json を読めませんでした");
            return null;
        }
    }

    /// <summary>
    /// auth.json は <c>{ "opencode-go": { "type": "api", "key": "..." } }</c> の形。
    /// </summary>
    public static string? ReadApiKey(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var providerId in ProviderIds)
            {
                if (!doc.RootElement.TryGetProperty(providerId, out var entry) || entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var key in new[] { "key", "apiKey", "api_key", "token", "access" })
                {
                    if (entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        var text = value.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
