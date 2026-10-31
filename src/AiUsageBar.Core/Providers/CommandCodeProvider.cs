using System.Net.Http.Headers;
using System.Text.Json;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core.Providers;

public static class CommandCodeProvider
{
    public const string CreditsPath = "/alpha/billing/credits";
    public const string SubscriptionsPath = "/alpha/billing/subscriptions";

    public static string CreditsUrl => CommandCodeAuth.BaseUrl + CreditsPath;

    public static string SubscriptionsUrl => CommandCodeAuth.BaseUrl + SubscriptionsPath;

    public static async Task<ProviderSnapshot> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var updated = Formatting.UtcNow();
        try
        {
            var token = CredentialStore.GetToken(CredentialUsers.CommandCode)
                ?? Environment.GetEnvironmentVariable(CommandCodeAuth.EnvVar)
                ?? CommandCodeAuth.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                AppLog.Info("Command Code のトークンが見つかりません");
                throw new AuthRequiredException("Command Code のトークンが見つかりません");
            }

            var credits = await SendAsync(http, CreditsUrl, token, cancellationToken).ConfigureAwait(false);
            var subscription = await TrySendAsync(http, SubscriptionsUrl, token, cancellationToken).ConfigureAwait(false);
            return FromPayload(credits, subscription, updated);
        }
        catch (AuthRequiredException ex)
        {
            AppLog.Info($"Command Code の認証が必要です: {CredentialStore.AsSafeError(ex)}");
            return new ProviderSnapshot("commandcode", "Command Code", false, UiText.CompactError("Command Code", ProviderErrors.AuthRequired), ["Command Code", UiText.CommandCodeAuthHint], ProviderErrors.AuthRequired, updated);
        }
        catch (RateLimitedException)
        {
            return new ProviderSnapshot("commandcode", "Command Code", false, UiText.CompactError("Command Code", ProviderErrors.RateLimited), ["Command Code", UiText.RateLimitedMessage], ProviderErrors.RateLimited, updated);
        }
        catch (FetchException ex)
        {
            AppLog.Error("Command Code の HTTP 取得に失敗しました", ex);
            return new ProviderSnapshot("commandcode", "Command Code", false, "Command Code --", ["Command Code", UiText.FetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
        catch (Exception ex)
        {
            AppLog.Error("Command Code の使用量取得で予期しないエラー", ex);
            return new ProviderSnapshot("commandcode", "Command Code", false, "Command Code --", ["Command Code", UiText.FetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
    }

    public static ProviderSnapshot FromPayload(JsonElement payload, JsonElement? subscription, DateTimeOffset updated)
    {
        var credits = Formatting.Object(payload, "credits") ?? payload;
        var limits = Formatting.Object(credits, "windowLimits") ?? Formatting.Object(credits, "window_limits") ?? default;
        var windows = new List<RateWindow>();
        AddWindow(windows, limits, "fiveHour", "5h", 5 * 60);
        AddWindow(windows, limits, "five_hour", "5h", 5 * 60);
        AddWindow(windows, limits, "weekly", "7d", 7 * 24 * 60);
        AddWindow(windows, limits, "week", "7d", 7 * 24 * 60);
        var remaining = RemainingCredits(credits);
        var plan = Formatting.PickStr(credits, "planId", "plan_id")
            ?? (subscription is { } sub ? Formatting.PickStr(sub, "planId", "plan_id") : null);

        var compactParts = new List<string> { "Command Code" };
        if (windows.Count > 0)
        {
            compactParts.Add(string.Join(" · ", windows.Select(item => item.Compact())));
        }
        else if (remaining is not null)
        {
            compactParts.Add(Money(remaining.Value));
        }
        else
        {
            compactParts.Add("--");
        }

        var lines = new List<string> { "Command Code" + (plan is null ? "" : $"  {plan}") };
        foreach (var window in windows)
        {
            var reset = Formatting.FormatReset(window.ResetsAt, updated);
            var percent = window.RemainingPercent is null ? "--" : Formatting.PercentRemaining(window.RemainingPercent.Value);
            var when = window.ResetsAt is null ? "" : UiText.ParentheticalTime(window.ResetsAt.Value.ToLocalTime().ToString("MM/dd HH:mm"));
            lines.Add(UiText.WindowDetail(window.Label, percent, reset, when));
        }

        if (remaining is not null)
        {
            lines.Add(UiText.Labeled(UiText.Credits, Money(remaining.Value)));
        }

        var sandbox = SandboxLine(credits);
        if (sandbox is not null)
        {
            lines.Add(sandbox);
        }

        lines.Add(UiText.Labeled(UiText.LastUpdated, updated.ToLocalTime().ToString("HH:mm:ss")));
        var metrics = windows.Select(window =>
        {
            var hint = window.ResetsAt is null ? null : UiText.ResetHint(Formatting.FormatReset(window.ResetsAt, updated));
            return new UsageMetric(window.Label, window.UsedPercent, hint);
        }).ToList();

        var usedPercents = windows.Select(item => item.UsedPercent).Where(value => value is not null).Select(value => value!.Value).ToList();
        var ok = windows.Count > 0 || remaining is not null;
        return new ProviderSnapshot(
            "commandcode",
            "Command Code",
            ok,
            string.Join(" ", compactParts),
            lines,
            ok ? null : ProviderErrors.Unparsed,
            updated,
            usedPercents.Count > 0 ? usedPercents.Max() : null,
            plan,
            metrics);
    }

    private static void AddWindow(List<RateWindow> windows, JsonElement limits, string key, string label, int durationMins)
    {
        if (limits.ValueKind != JsonValueKind.Object || !limits.TryGetProperty(key, out var value))
        {
            return;
        }

        var window = WindowReader.Read(value, label, durationMins);
        if (window is not null && windows.All(item => item.Label != window.Label))
        {
            windows.Add(window);
        }
    }

    private static double? RemainingCredits(JsonElement credits)
    {
        var monthly = Formatting.NumberOrString(credits, "monthlyCredits", "monthly_credits");
        var purchased = Formatting.NumberOrString(credits, "purchasedCredits", "purchased_credits");
        var free = Formatting.NumberOrString(credits, "freeCredits", "free_credits");
        var total = (monthly ?? 0) + (purchased ?? 0) + (free ?? 0);
        return monthly is null && purchased is null && free is null ? null : Math.Max(0, total);
    }

    private static string? SandboxLine(JsonElement credits)
    {
        var minutes = Formatting.Object(credits, "sandboxMinutes") ?? Formatting.Object(credits, "sandbox_minutes");
        if (minutes is not { } sandbox)
        {
            return null;
        }

        var used = Formatting.NumberOrString(sandbox, "usedMinutes", "used_minutes");
        var limit = Formatting.NumberOrString(sandbox, "limitMinutes", "limit_minutes");
        if (used is null && limit is null)
        {
            return null;
        }

        var text = used is null || limit is null
            ? $"{used ?? 0:0}m"
            : $"{used:0}/{limit:0}m";
        return UiText.Labeled(UiText.CloudMinutes, text);
    }

    private static string Money(double value) => "$" + value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement?> TrySendAsync(HttpClient http, string url, string token, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(http, url, token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FetchException or AuthRequiredException or RateLimitedException)
        {
            AppLog.Info($"Command Code の {new Uri(url).AbsolutePath} は取得できませんでした");
            return null;
        }
    }

    private static async Task<JsonElement> SendAsync(HttpClient http, string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "ai-usage-bar");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException("Command Code の HTTP 取得に失敗しました", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403)
            {
                AppLog.Info($"Command Code HTTP status={status} path={new Uri(url).AbsolutePath}");
                throw new AuthRequiredException("Command Code の認証が無効です");
            }

            if (status == 429)
            {
                throw new RateLimitedException();
            }

            if (status >= 400)
            {
                throw new FetchException($"Command Code HTTP status={status}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (JsonException ex)
            {
                throw new FetchException("Command Code の応答が JSON ではありません", ex);
            }

            var payload = doc.RootElement.Clone();
            doc.Dispose();
            if (payload.ValueKind != JsonValueKind.Object)
            {
                throw new FetchException("Command Code の応答形式が想定と違います");
            }

            return payload;
        }
    }
}
