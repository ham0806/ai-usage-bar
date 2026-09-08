using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiUsageBar.Core.Auth;

public sealed record CodexRefreshResult(string AccessToken, string? RefreshToken, string? IdToken);

public static class CodexAuth
{
    public const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    public const string TokenUrl = "https://auth.openai.com/oauth/token";

    public static string AuthJsonPath
    {
        get
        {
            var home = Environment.GetEnvironmentVariable("CODEX_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            return Path.Combine(home, "auth.json");
        }
    }

    public static string RefreshRequestJson(string refreshToken) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });

    public static Dictionary<string, string> LoadTokens()
    {
        var path = AuthJsonPath;
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var root = doc.RootElement;
            var source = root.TryGetProperty("tokens", out var nested) && nested.ValueKind == JsonValueKind.Object
                ? nested
                : root;
            var access = Formatting.PickStr(source, "access_token", "accessToken");
            var refresh = Formatting.PickStr(source, "refresh_token", "refreshToken");
            var idToken = Formatting.PickStr(source, "id_token", "idToken");
            var account = Formatting.PickStr(source, "account_id", "accountId")
                ?? AccountIdFromJwt(idToken)
                ?? AccountIdFromJwt(access);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (access is not null)
            {
                result["access_token"] = access;
            }

            if (refresh is not null)
            {
                result["refresh_token"] = refresh;
            }

            if (account is not null)
            {
                result["account_id"] = account;
            }

            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            AppLog.Warning("Codex の auth.json を読めませんでした");
            return [];
        }
    }

    public static bool AccessTokenNeedsRefresh(string accessToken, DateTimeOffset? now = null)
    {
        var payload = Formatting.JwtPayload(accessToken);
        if (!payload.TryGetValue("exp", out var expEl) || Formatting.Number(expEl) is not { } exp)
        {
            return false;
        }

        now ??= Formatting.UtcNow();
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)exp) <= now.Value.AddMinutes(2);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static string? AccountIdFromJwt(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var payload = Formatting.JwtPayload(token);
        if (payload.TryGetValue("https://api.openai.com/auth", out var claim) && claim.ValueKind == JsonValueKind.Object)
        {
            return Formatting.PickStr(claim, "chatgpt_account_id", "account_id");
        }

        return Formatting.PickStr(payload, "chatgpt_account_id", "account_id");
    }

    public static async Task<CodexRefreshResult?> RefreshAccessTokenAsync(string refreshToken, HttpClient http, CancellationToken cancellationToken)
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
                AppLog.Warning($"Codex のトークン更新が拒否されました status={(int)response.StatusCode}");
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

            return new CodexRefreshResult(
                access,
                Formatting.PickStr(doc.RootElement, "refresh_token"),
                Formatting.PickStr(doc.RootElement, "id_token"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            AppLog.Warning("Codex のトークン更新リクエストに失敗しました");
            return null;
        }
    }

    public static void ApplyRefreshedTokens(CodexRefreshResult tokens, string? path = null)
    {
        path ??= AuthJsonPath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
            var nested = root["tokens"] as JsonObject ?? [];
            nested["access_token"] = tokens.AccessToken;
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                nested["refresh_token"] = tokens.RefreshToken;
            }

            if (!string.IsNullOrEmpty(tokens.IdToken))
            {
                nested["id_token"] = tokens.IdToken;
            }

            var account = AccountIdFromJwt(tokens.AccessToken) ?? AccountIdFromJwt(tokens.IdToken);
            if (!string.IsNullOrEmpty(account))
            {
                nested["account_id"] = account;
            }

            root["tokens"] = nested;
            root["last_refresh"] = Formatting.UtcNow().ToString("o");
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            AppLog.Info("Codex のトークンを更新して保存しました");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            AppLog.Warning("Codex の auth.json へ更新トークンを書けませんでした");
        }
    }

    public static IReadOnlyList<string>? FindCli()
    {
        var names = new[] { "codex.cmd", "codex.exe", "codex.bat", "codex" };
        foreach (var dir in CliSearchDirs())
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (candidate.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    return ["powershell.exe", "-NoProfile", "-NonInteractive", "-File", candidate];
                }

                return [candidate];
            }
        }

        return null;
    }

    private static IEnumerable<string> CliSearchDirs()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var extra in new[]
                 {
                     Path.Combine(local, "mise", "shims"),
                     Path.Combine(home, ".local", "bin"),
                     Path.Combine(home, ".cargo", "bin"),
                 })
        {
            if (seen.Add(extra))
            {
                yield return extra;
            }
        }
    }
}
