using System.Net.Http.Headers;
using System.Text.Json;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core.Providers;

public static class ClaudeProvider
{
    public const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    public const string OAuthBeta = "oauth-2025-04-20";
    public const string UserAgent = "claude-cli/2.0.0 (external, cli)";

    private static readonly (string Key, string Label, int DurationMins)[] Aliases =
    [
        ("five_hour", "5h", 5 * 60),
        ("seven_day", "7d", 7 * 24 * 60),
        ("seven_day_opus", "Opus", 7 * 24 * 60),
        ("seven_day_sonnet", "Sonnet", 7 * 24 * 60),
        ("seven_day_haiku", "Haiku", 7 * 24 * 60),
        ("seven_day_oauth_apps", "Apps", 7 * 24 * 60),
    ];

    public static async Task<ProviderSnapshot> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var updated = Formatting.UtcNow();
        try
        {
            var token = await ResolveTokenAsync(http, cancellationToken).ConfigureAwait(false);
            var payload = await SendUsageAsync(http, token, cancellationToken).ConfigureAwait(false);
            return FromPayload(payload, updated);
        }
        catch (AuthRequiredException ex)
        {
            AppLog.Info($"Claude の認証が必要です: {CredentialStore.AsSafeError(ex)}");
            return new ProviderSnapshot("claude", "Claude", false, UiText.CompactError("Claude", ProviderErrors.AuthRequired), ["Claude", UiText.ClaudeAuthHint], ProviderErrors.AuthRequired, updated);
        }
        catch (RateLimitedException)
        {
            return new ProviderSnapshot("claude", "Claude", false, UiText.CompactError("Claude", ProviderErrors.RateLimited), ["Claude", UiText.RateLimitedMessage], ProviderErrors.RateLimited, updated);
        }
        catch (FetchException ex)
        {
            AppLog.Error("Claude の HTTP 取得に失敗しました", ex);
            return new ProviderSnapshot("claude", "Claude", false, "Claude --", ["Claude", UiText.FetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
        catch (Exception ex)
        {
            AppLog.Error("Claude の使用量取得で予期しないエラー", ex);
            return new ProviderSnapshot("claude", "Claude", false, "Claude --", ["Claude", UiText.FetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
    }

    public static IReadOnlyList<RateWindow> CollectWindows(JsonElement payload)
    {
        var windows = new List<RateWindow>();
        foreach (var container in WindowReader.Containers(payload, "limits", "usage", "rate_limits"))
        {
            foreach (var (key, label, duration) in Aliases)
            {
                if (!container.TryGetProperty(key, out var value))
                {
                    continue;
                }

                var window = WindowReader.Read(value, label, duration);
                if (window is not null && windows.All(item => item.Label != window.Label))
                {
                    windows.Add(window);
                }
            }
        }

        return windows;
    }

    public static ProviderSnapshot FromPayload(JsonElement payload, DateTimeOffset updated)
    {
        var windows = CollectWindows(payload);
        var extra = ExtraUsage(payload);
        var compactParts = new List<string> { "Claude" };
        if (windows.Count > 0)
        {
            compactParts.Add(string.Join(" · ", windows.Select(item => item.Compact())));
        }
        else if (extra is not null)
        {
            compactParts.Add(Formatting.Percent(extra.Value));
        }
        else
        {
            compactParts.Add("--");
        }

        var lines = new List<string> { "Claude" };
        foreach (var window in windows)
        {
            var reset = Formatting.FormatReset(window.ResetsAt, updated);
            var percent = window.RemainingPercent is null ? "--" : Formatting.Percent(window.RemainingPercent.Value);
            var when = window.ResetsAt is null ? "" : UiText.ParentheticalTime(window.ResetsAt.Value.ToLocalTime().ToString("MM/dd HH:mm"));
            lines.Add(UiText.WindowDetail(window.Label, percent, reset, when));
        }

        if (extra is not null && ExtraEnabled(payload))
        {
            lines.Add(UiText.Labeled(UiText.ExtraUsage, Formatting.Percent(extra.Value)));
        }

        lines.Add(UiText.Labeled(UiText.LastUpdated, updated.ToLocalTime().ToString("HH:mm:ss")));
        var metrics = windows.Select(window =>
        {
            var remaining = window.RemainingPercent is null ? null : UiText.RemainingHint(Formatting.Percent(window.RemainingPercent.Value));
            var reset = window.ResetsAt is null ? null : UiText.ResetHint(Formatting.FormatReset(window.ResetsAt, updated));
            var hint = string.Join(" · ", new[] { remaining, reset }.Where(part => !string.IsNullOrEmpty(part)));
            return new UsageMetric(window.Label, window.UsedPercent, string.IsNullOrEmpty(hint) ? null : hint);
        }).ToList();

        var usedPercents = windows.Select(item => item.UsedPercent).Where(value => value is not null).Select(value => value!.Value).ToList();
        double? usedPercent = usedPercents.Count > 0 ? usedPercents.Max() : extra;
        var ok = windows.Count > 0 || extra is not null;
        return new ProviderSnapshot(
            "claude",
            "Claude",
            ok,
            string.Join(" ", compactParts),
            lines,
            ok ? null : ProviderErrors.Unparsed,
            updated,
            usedPercent,
            null,
            metrics);
    }

    private static async Task<string> ResolveTokenAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var saved = CredentialStore.GetToken(CredentialUsers.Claude);
        if (!string.IsNullOrEmpty(saved))
        {
            return saved;
        }

        var credentials = ClaudeAuth.Load();
        if (credentials is null)
        {
            AppLog.Info("Claude の .credentials.json が見つかりません");
            throw new AuthRequiredException("Claude の認証情報が見つかりません");
        }

        if (ClaudeAuth.NeedsRefresh(credentials) && credentials.RefreshToken is { } refresh)
        {
            AppLog.Info("Claude の access_token の期限が近いので更新します");
            var refreshed = await ClaudeAuth.RefreshAsync(refresh, http, cancellationToken).ConfigureAwait(false);
            if (refreshed is not null)
            {
                ClaudeAuth.ApplyRefreshed(refreshed);
                return refreshed.AccessToken;
            }
        }

        if (string.IsNullOrEmpty(credentials.AccessToken))
        {
            throw new AuthRequiredException("Claude の access_token がありません");
        }

        return credentials.AccessToken;
    }

    private static async Task<JsonElement> SendUsageAsync(HttpClient http, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBeta);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException("Claude の HTTP 取得に失敗しました", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403)
            {
                AppLog.Info($"Claude HTTP status={status} path={new Uri(UsageUrl).AbsolutePath}");
                throw new AuthRequiredException("Claude の認証が無効です");
            }

            if (status == 429)
            {
                throw new RateLimitedException();
            }

            if (status >= 400)
            {
                throw new FetchException($"Claude HTTP status={status}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (JsonException ex)
            {
                throw new FetchException("Claude の応答が JSON ではありません", ex);
            }

            var payload = doc.RootElement.Clone();
            doc.Dispose();
            if (payload.ValueKind != JsonValueKind.Object)
            {
                throw new FetchException("Claude の応答形式が想定と違います");
            }

            return payload;
        }
    }

    private static bool ExtraEnabled(JsonElement payload)
    {
        if (Formatting.Object(payload, "extra_usage") is not { } extra)
        {
            return false;
        }

        return extra.TryGetProperty("is_enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
    }

    private static double? ExtraUsage(JsonElement payload)
    {
        if (Formatting.Object(payload, "extra_usage") is not { } extra)
        {
            return null;
        }

        return WindowReader.UsedPercent(extra);
    }
}
