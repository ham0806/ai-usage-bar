using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core.Providers;

public static class CursorProvider
{
    public const string UsageUrl = "https://cursor.com/api/usage-summary";
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public static async Task<ProviderSnapshot> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var updated = Formatting.UtcNow();
        try
        {
            var cookie = CursorAuth.LoadCursorCookie();
            if (string.IsNullOrEmpty(cookie))
            {
                AppLog.Info("Cursor のセッショントークンが見つかりません");
                throw new AuthRequiredException("Cursor のセッショントークンが見つかりません");
            }

            JsonElement payload;
            try
            {
                payload = await FetchSummaryAsync(http, cookie, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthRequiredException)
            {
                var saved = CredentialStore.GetCursorToken();
                var fallback = saved is null ? null : CursorAuth.NormalizeSessionCookie(saved);
                if (string.IsNullOrEmpty(fallback) || fallback == cookie)
                {
                    throw;
                }

                payload = await FetchSummaryAsync(http, fallback, cancellationToken).ConfigureAwait(false);
            }

            return FromPayload(payload, updated);
        }
        catch (AuthRequiredException ex)
        {
            return new ProviderSnapshot(
                "cursor",
                "Cursor",
                false,
                "Cursor 要認証",
                [
                    "Cursor",
                    CredentialStore.AsSafeError(ex),
                    "cursor.com にログインするか、設定から Cookie を貼り付けてください",
                ],
                "要認証",
                updated);
        }
        catch (RateLimitedException)
        {
            return new ProviderSnapshot(
                "cursor",
                "Cursor",
                false,
                "Cursor 制限中",
                ["Cursor", "レート制限中です。しばらく待ってから再取得します"],
                "レート制限",
                updated);
        }
        catch (FetchException ex)
        {
            return new ProviderSnapshot("cursor", "Cursor", false, "Cursor --", ["Cursor", CredentialStore.AsSafeError(ex)], CredentialStore.AsSafeError(ex), updated);
        }
        catch (Exception ex)
        {
            AppLog.Error("Cursor の使用量取得で予期しないエラー", ex);
            return new ProviderSnapshot("cursor", "Cursor", false, "Cursor --", ["Cursor", "取得に失敗しました"], "取得失敗", updated);
        }
    }

    public static ProviderSnapshot FromPayload(JsonElement payload, DateTimeOffset updated)
    {
        var membership = Formatting.PickStr(payload, "membershipType", "plan", "membership");
        var unlimited = payload.TryGetProperty("isUnlimited", out var unlimitedEl) && unlimitedEl.ValueKind == JsonValueKind.True;
        var individual = Formatting.Object(payload, "individualUsage") ?? default;
        var plan = individual.ValueKind == JsonValueKind.Object ? Formatting.Object(individual, "plan") ?? default : default;
        var onDemand = individual.ValueKind == JsonValueKind.Object ? Formatting.Object(individual, "onDemand") ?? default : default;
        if (plan.ValueKind != JsonValueKind.Object)
        {
            plan = Formatting.Object(payload, "plan") ?? default;
        }

        var auto = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "autoPercentUsed") : null;
        var api = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "apiPercentUsed") : null;
        auto ??= PercentFromMessage(Formatting.PickStr(payload, "autoModelSelectedDisplayMessage"));
        api ??= PercentFromMessage(Formatting.PickStr(payload, "namedModelSelectedDisplayMessage"));
        var total = plan.ValueKind == JsonValueKind.Object
            ? Formatting.Number(plan, "totalPercentUsed")
            : Formatting.Number(payload, "totalPercentUsed");
        total ??= plan.ValueKind == JsonValueKind.Object ? PlanPercent(plan) : null;

        string compact;
        double? usedPercent;
        if (unlimited)
        {
            compact = "Cursor ∞";
            usedPercent = null;
        }
        else
        {
            var parts = new List<string>();
            if (auto is not null)
            {
                parts.Add($"Auto {Formatting.Percent(auto.Value)}");
            }

            if (api is not null)
            {
                parts.Add($"API {Formatting.Percent(api.Value)}");
            }

            if (parts.Count > 0)
            {
                compact = "Cursor " + string.Join(" · ", parts);
                usedPercent = new[] { auto, api }.Where(value => value is not null).Select(value => value!.Value).Max();
            }
            else if (total is not null)
            {
                compact = $"Cursor {Formatting.Percent(total.Value)}";
                usedPercent = total;
            }
            else
            {
                compact = "Cursor --";
                usedPercent = null;
            }
        }

        var lines = new List<string> { "Cursor" + (membership is null ? "" : $"  {membership}") };
        if (unlimited)
        {
            lines.Add("プラン  無制限");
        }
        else
        {
            if (auto is not null)
            {
                lines.Add($"Auto  {Formatting.Percent(auto.Value)}");
            }

            if (api is not null)
            {
                lines.Add($"API  {Formatting.Percent(api.Value)}");
            }

            if (auto is null && api is null && total is not null)
            {
                var used = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "used") : null;
                var limit = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "limit") : null;
                var extra = used is not null && limit is not null && limit > 0 ? $"（{used:0}/{limit:0}）" : "";
                lines.Add($"プラン  {Formatting.Percent(total.Value)}{extra}");
            }
        }

        var cycle = FormatCycle(
            payload.TryGetProperty("billingCycleStart", out var start) ? Formatting.ParseTimestamp(start) : null,
            payload.TryGetProperty("billingCycleEnd", out var end) ? Formatting.ParseTimestamp(end) : null);
        if (cycle is not null)
        {
            lines.Add($"課金周期  {cycle}");
        }

        if (onDemand.ValueKind == JsonValueKind.Object && onDemand.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True)
        {
            var used = Formatting.Number(onDemand, "used");
            var limit = Formatting.Number(onDemand, "limit");
            if (used is not null && limit is not null)
            {
                lines.Add($"オンデマンド  {used:0}/{limit:0}");
            }
            else if (used is not null)
            {
                lines.Add($"オンデマンド  {used:0}");
            }
        }

        lines.Add($"最終更新  {updated.ToLocalTime():HH:mm:ss}");
        var metrics = new List<UsageMetric>();
        if (unlimited)
        {
            metrics.Add(new UsageMetric("プラン", null, "無制限"));
        }
        else
        {
            if (auto is not null)
            {
                metrics.Add(new UsageMetric("Auto", auto));
            }

            if (api is not null)
            {
                metrics.Add(new UsageMetric("API", api));
            }

            if (auto is null && api is null && total is not null)
            {
                var used = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "used") : null;
                var limit = plan.ValueKind == JsonValueKind.Object ? Formatting.Number(plan, "limit") : null;
                var extra = used is not null && limit is not null && limit > 0 ? $"{used:0}/{limit:0}" : null;
                metrics.Add(new UsageMetric("プラン", total, extra));
            }
        }

        var ok = unlimited || auto is not null || api is not null || total is not null;
        return new ProviderSnapshot(
            "cursor",
            "Cursor",
            ok,
            compact,
            lines,
            ok ? null : "使用量を解釈できませんでした",
            updated,
            usedPercent,
            membership,
            metrics);
    }

    private static async Task<JsonElement> FetchSummaryAsync(HttpClient http, string cookie, CancellationToken cancellationToken)
    {
        try
        {
            return await SendSummaryAsync(http, cookie, bearer: false, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthRequiredException)
        {
            AppLog.Info("Cursor の Cookie 認証が無効なので Bearer を試します");
            return await SendSummaryAsync(http, CursorAuth.AccessToken(cookie), bearer: true, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<JsonElement> SendSummaryAsync(HttpClient http, string token, bool bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        if (bearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"WorkosCursorSessionToken={token}");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Origin", "https://cursor.com");
        request.Headers.Referrer = new Uri("https://cursor.com/dashboard/usage");
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException("Cursor の HTTP 取得に失敗しました", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403)
            {
                AppLog.Info($"Cursor HTTP status={status} ({(bearer ? "bearer" : "cookie")})");
                throw new AuthRequiredException("Cursor のセッショントークンが無効です");
            }

            if (status == 429)
            {
                throw new RateLimitedException();
            }

            if (status >= 400)
            {
                throw new FetchException($"Cursor HTTP status={status}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (JsonException ex)
            {
                throw new FetchException("Cursor の応答が JSON ではありません", ex);
            }

            var payload = doc.RootElement.Clone();
            doc.Dispose();
            if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String && error.GetString() == "not_authenticated")
            {
                AppLog.Info($"Cursor の応答が未認証です ({(bearer ? "bearer" : "cookie")})");
                throw new AuthRequiredException("Cursor のセッショントークンが無効です");
            }

            if (payload.ValueKind != JsonValueKind.Object)
            {
                throw new FetchException("Cursor の応答形式が想定と違います");
            }

            return payload;
        }
    }

    private static double? PercentFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"(\d+(?:\.\d+)?)\s*%");
        return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? PlanPercent(JsonElement plan)
    {
        foreach (var key in new[] { "totalPercentUsed", "apiPercentUsed", "autoPercentUsed" })
        {
            var value = Formatting.Number(plan, key);
            if (value is not null)
            {
                return value;
            }
        }

        var used = Formatting.Number(plan, "used");
        var limit = Formatting.Number(plan, "limit");
        if (used is not null && limit is > 0)
        {
            return used / limit * 100.0;
        }

        var remaining = Formatting.Number(plan, "remaining");
        if (used is not null && remaining is not null && used + remaining > 0)
        {
            return used / (used + remaining) * 100.0;
        }

        return null;
    }

    private static string? FormatCycle(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null && end is null)
        {
            return null;
        }

        string Fmt(DateTimeOffset? value) => value is null ? "?" : value.Value.ToLocalTime().ToString("yyyy-MM-dd");
        return $"{Fmt(start)} ～ {Fmt(end)}";
    }
}
