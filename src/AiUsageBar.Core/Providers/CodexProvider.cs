using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core.Providers;

public static class CodexProvider
{
    public const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    public const string CodexUsageUrl = "https://chatgpt.com/backend-api/codex/usage";
    public const string UserAgent = "codex-cli";

    private static readonly string[] UsageUrls = [UsageUrl, CodexUsageUrl];

    private static readonly string[] WindowLabelOrder = ["5h", "1d", "7d", "30d", "1y"];

    private static readonly (string Key, string FallbackLabel)[] WindowAliases =
    [
        ("primary", "5h"),
        ("secondary", "7d"),
        ("primary_window", "5h"),
        ("secondary_window", "7d"),
        ("five_hour", "5h"),
        ("fiveHour", "5h"),
        ("five_hour_limit", "5h"),
        ("seven_day", "7d"),
        ("sevenDay", "7d"),
        ("weekly", "7d"),
        ("weekly_limit", "7d"),
    ];

    public static async Task<ProviderSnapshot> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var updated = Formatting.UtcNow();
        try
        {
            var payload = await FetchPayloadAsync(http, cancellationToken).ConfigureAwait(false);
            return FromPayload(payload, updated);
        }
        catch (AuthRequiredException ex)
        {
            AppLog.Info($"Codex の認証が必要です: {CredentialStore.AsSafeError(ex)}");
            return new ProviderSnapshot("codex", "Codex", false, "Codex 要認証", ["Codex", CredentialStore.AsSafeError(ex)], "要認証", updated);
        }
        catch (RateLimitedException)
        {
            return new ProviderSnapshot("codex", "Codex", false, "Codex 制限中", ["Codex", "レート制限中です。しばらく待ってから再取得します"], "レート制限", updated);
        }
        catch (FetchException ex)
        {
            return new ProviderSnapshot("codex", "Codex", false, "Codex --", ["Codex", CredentialStore.AsSafeError(ex)], CredentialStore.AsSafeError(ex), updated);
        }
        catch (Exception ex)
        {
            AppLog.Error("Codex の使用量取得で予期しないエラー", ex);
            return new ProviderSnapshot("codex", "Codex", false, "Codex --", ["Codex", "取得に失敗しました"], "取得失敗", updated);
        }
    }

    public static ProviderSnapshot FromPayload(JsonElement payload, DateTimeOffset updated)
    {
        var limits = ExtractRateLimits(payload);
        var windows = CollectWindows(payload);
        var credits = ObjectOrEmpty(limits, "credits");
        if (credits.ValueKind != JsonValueKind.Object)
        {
            credits = Formatting.Object(payload, "credits") ?? default;
        }

        var plan = Formatting.PickStr(limits, "planType", "plan_type")
            ?? Formatting.PickStr(payload, "planType", "plan_type");
        var compactParts = new List<string> { "Codex" };
        if (windows.Count > 0)
        {
            compactParts.Add(string.Join(" · ", windows.Select(item => item.Compact())));
        }
        else if (!string.IsNullOrEmpty(plan))
        {
            compactParts.Add(plan);
        }
        else
        {
            compactParts.Add("--");
        }

        var lines = new List<string> { "Codex" + (plan is null ? "" : $"  {plan}") };
        foreach (var window in windows)
        {
            var reset = Formatting.FormatReset(window.ResetsAt, updated);
            var percent = window.RemainingPercent is null ? "--" : Formatting.Percent(window.RemainingPercent.Value);
            var when = window.ResetsAt is null ? "" : $"（{window.ResetsAt.Value.ToLocalTime():MM/dd HH:mm}）";
            lines.Add($"{window.Label}  残り {percent}  リセット {reset}{when}");
        }

        var creditLine = CreditLine(credits);
        if (creditLine is not null)
        {
            lines.Add(creditLine);
        }

        lines.Add($"最終更新  {updated.ToLocalTime():HH:mm:ss}");
        var metrics = windows.Select(window =>
        {
            var remaining = window.RemainingPercent is null ? null : $"残り {Formatting.Percent(window.RemainingPercent.Value)}";
            var reset = window.ResetsAt is null ? null : $"リセット {Formatting.FormatReset(window.ResetsAt, updated)}";
            var hint = string.Join(" · ", new[] { remaining, reset }.Where(part => !string.IsNullOrEmpty(part)));
            return new UsageMetric(window.Label, window.UsedPercent, string.IsNullOrEmpty(hint) ? null : hint);
        }).ToList();
        var usedPercent = windows.Count > 0 ? windows[0].UsedPercent : null;
        return new ProviderSnapshot(
            "codex",
            "Codex",
            true,
            string.Join(" ", compactParts),
            lines,
            null,
            updated,
            usedPercent,
            plan,
            metrics);
    }

    public static IReadOnlyList<RateWindow> CollectWindows(JsonElement payload)
    {
        var windows = new List<RateWindow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in SourceDicts(payload))
        {
            foreach (var (key, fallbackLabel) in WindowAliases)
            {
                if (source.TryGetProperty(key, out var value))
                {
                    var window = WindowFrom(value, fallbackLabel);
                    if (window is not null && seen.Add(window.Label))
                    {
                        windows.Add(window);
                    }
                }
            }

            foreach (var listKey in new[] { "windows", "rate_limit_windows" })
            {
                if (!source.TryGetProperty(listKey, out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var window = WindowFrom(item, "win");
                    if (window is not null && seen.Add(window.Label))
                    {
                        windows.Add(window);
                    }
                }
            }
        }

        return windows
            .OrderBy(window =>
            {
                var index = Array.IndexOf(WindowLabelOrder, window.Label);
                return index < 0 ? WindowLabelOrder.Length : index;
            })
            .ToList();
    }

    private static async Task<JsonElement> FetchPayloadAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var tokens = CodexAuth.LoadTokens();
        JsonElement? httpPayload = null;
        Exception? httpError = null;
        if (tokens.TryGetValue("refresh_token", out var pendingRefresh)
            && tokens.TryGetValue("access_token", out var pendingAccess)
            && CodexAuth.AccessTokenNeedsRefresh(pendingAccess))
        {
            AppLog.Info("Codex の access_token の期限が近いので更新します");
            var refreshed = await CodexAuth.RefreshAccessTokenAsync(pendingRefresh, http, cancellationToken).ConfigureAwait(false);
            if (refreshed is not null)
            {
                CodexAuth.ApplyRefreshedTokens(refreshed);
                tokens = CodexAuth.LoadTokens();
            }
        }

        if (tokens.TryGetValue("access_token", out var access))
        {
            try
            {
                tokens.TryGetValue("account_id", out var account);
                httpPayload = await FetchHttpAsync(http, access, account, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthRequiredException ex)
            {
                if (tokens.TryGetValue("refresh_token", out var refresh))
                {
                    var newTokens = await CodexAuth.RefreshAccessTokenAsync(refresh, http, cancellationToken).ConfigureAwait(false);
                    if (newTokens is not null)
                    {
                        CodexAuth.ApplyRefreshedTokens(newTokens);
                        try
                        {
                            var latest = CodexAuth.LoadTokens();
                            latest.TryGetValue("account_id", out var account);
                            httpPayload = await FetchHttpAsync(http, newTokens.AccessToken, account, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception retryEx)
                        {
                            httpError = retryEx;
                        }
                    }
                    else
                    {
                        httpError = ex;
                    }
                }
                else
                {
                    httpError = ex;
                }
            }
            catch (Exception ex) when (ex is RateLimitedException or FetchException)
            {
                httpError = ex;
                AppLog.Info("Codex HTTP 取得に失敗したので app-server に切り替えます");
            }
        }
        else
        {
            AppLog.Info("Codex の auth.json に access_token がありません");
        }

        if (httpPayload is { } payload && CollectWindows(payload).Count > 0)
        {
            AppLog.Info("Codex の使用量を HTTP で取得しました");
            return payload;
        }

        if (httpPayload is not null)
        {
            AppLog.Info("Codex HTTP 応答にレート枠が無いので app-server を試します");
        }

        try
        {
            var rpcPayload = await FetchAppServerAsync(cancellationToken).ConfigureAwait(false);
            if (CollectWindows(rpcPayload).Count > 0 || httpPayload is null)
            {
                return rpcPayload;
            }

            return httpPayload.Value;
        }
        catch (Exception ex)
        {
            if (httpPayload is not null)
            {
                return httpPayload.Value;
            }

            if (httpError is AuthRequiredException or RateLimitedException)
            {
                throw httpError;
            }

            if (ex is AuthRequiredException or RateLimitedException or FetchException)
            {
                throw;
            }

            throw new FetchException("Codex の使用量を取得できませんでした", ex);
        }
    }

    private static async Task<JsonElement> FetchHttpAsync(HttpClient http, string accessToken, string? accountId, CancellationToken cancellationToken)
    {
        FetchException? missing = null;
        AuthRequiredException? auth = null;
        foreach (var url in UsageUrls)
        {
            try
            {
                return await FetchHttpUrlAsync(http, url, accessToken, accountId, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthRequiredException ex)
            {
                auth = ex;
            }
            catch (FetchException ex) when (IsMissingEndpoint(ex))
            {
                missing = ex;
            }
        }

        if (auth is not null)
        {
            throw auth;
        }

        throw missing ?? new FetchException("Codex の HTTP 取得に失敗しました");
    }

    private static bool IsMissingEndpoint(FetchException ex) =>
        ex.Message.Contains("status=404", StringComparison.Ordinal)
        || ex.Message.Contains("status=405", StringComparison.Ordinal);

    private static async Task<JsonElement> FetchHttpUrlAsync(
        HttpClient http,
        string url,
        string accessToken,
        string? accountId,
        CancellationToken cancellationToken)
    {
        AuthRequiredException? auth = null;
        var accounts = new List<string?>();
        var jwtAccount = CodexAuth.AccountIdFromJwt(accessToken);
        if (!string.IsNullOrEmpty(jwtAccount))
        {
            accounts.Add(jwtAccount);
        }

        if (!string.IsNullOrEmpty(accountId) && !string.Equals(accountId, jwtAccount, StringComparison.Ordinal))
        {
            accounts.Add(accountId);
        }

        accounts.Add(null);
        foreach (var account in accounts)
        {
            try
            {
                return await SendUsageAsync(http, url, accessToken, account, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthRequiredException ex)
            {
                auth = ex;
            }
        }

        throw auth ?? new AuthRequiredException("Codex の認証が無効です");
    }

    private static async Task<JsonElement> SendUsageAsync(
        HttpClient http,
        string url,
        string accessToken,
        string? accountId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("originator", "codex_cli_rs");
        request.Headers.TryAddWithoutValidation("OAI-Product-Sku", "codex");
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(accountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException("Codex の HTTP 取得に失敗しました", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403)
            {
                var path = new Uri(url).AbsolutePath;
                var reason = SafeHttpError(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                var detail = string.IsNullOrEmpty(reason) ? "" : $" reason={reason}";
                AppLog.Info($"Codex HTTP status={status} path={path} account={(accountId is null ? "no" : "yes")}{detail}");
                throw new AuthRequiredException("Codex の認証が無効です");
            }

            if (status == 429)
            {
                double? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta is { } delta)
                {
                    retryAfter = delta.TotalSeconds;
                }

                throw new RateLimitedException(retryAfter);
            }

            if (status >= 400)
            {
                throw new FetchException($"Codex HTTP status={status}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (JsonException ex)
            {
                throw new FetchException("Codex の応答が JSON ではありません", ex);
            }

            var payload = doc.RootElement.Clone();
            doc.Dispose();
            if (payload.ValueKind != JsonValueKind.Object)
            {
                throw new FetchException("Codex の応答形式が想定と違います");
            }

            return payload;
        }
    }

    private static IReadOnlyList<string>? CodexCommand() => CodexAuth.FindCli();

    private static async Task<JsonElement> FetchAppServerAsync(CancellationToken cancellationToken)
    {
        var command = CodexCommand();
        if (command is null)
        {
            AppLog.Info("codex CLI が見つからないので app-server は使いません");
            throw new AuthRequiredException("codex CLI が見つかりません。codex login を実行してください");
        }

        var psi = new ProcessStartInfo
        {
            FileName = command[0],
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in command.Skip(1))
        {
            psi.ArgumentList.Add(arg);
        }

        psi.ArgumentList.Add("app-server");

        Process proc;
        try
        {
            proc = Process.Start(psi) ?? throw new FetchException("codex app-server を起動できませんでした");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new FetchException("codex app-server を起動できませんでした", ex);
        }

        var lines = new System.Collections.Concurrent.BlockingCollection<string>();
        var reader = Task.Run(() =>
        {
            try
            {
                while (proc.StandardOutput.ReadLine() is { } line)
                {
                    lines.Add(line);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                lines.CompleteAdding();
            }
        }, CancellationToken.None);

        try
        {
            await SendLineAsync(proc, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"clientInfo":{"name":"ai-usage-bar","title":"AI Usage Bar","version":"0.1.0"}}}""").ConfigureAwait(false);
            var init = WaitId(lines, 1, TimeSpan.FromSeconds(15), cancellationToken);
            if (init.TryGetProperty("error", out _))
            {
                throw new FetchException("codex app-server の初期化に失敗しました");
            }

            await SendLineAsync(proc, """{"jsonrpc":"2.0","method":"initialized","params":{}}""").ConfigureAwait(false);
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            await SendLineAsync(proc, """{"jsonrpc":"2.0","id":2,"method":"account/rateLimits/read"}""").ConfigureAwait(false);
            var result = WaitId(lines, 2, TimeSpan.FromSeconds(15), cancellationToken);
            if (result.TryGetProperty("error", out _))
            {
                throw new FetchException("Codex のレート制限を読めませんでした");
            }

            if (!result.TryGetProperty("result", out var payload) || payload.ValueKind != JsonValueKind.Object)
            {
                throw new FetchException("Codex app-server の応答形式が想定と違います");
            }

            return payload.Clone();
        }
        finally
        {
            try
            {
                proc.StandardInput.Close();
            }
            catch (Exception)
            {
            }

            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
            }

            try
            {
                await reader.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            proc.Dispose();
        }
    }

    private static async Task SendLineAsync(Process proc, string json)
    {
        await proc.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
        await proc.StandardInput.FlushAsync().ConfigureAwait(false);
    }

    private static JsonElement WaitId(System.Collections.Concurrent.BlockingCollection<string> lines, int expected, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            if (!lines.TryTake(out var line, (int)Math.Clamp(remaining.TotalMilliseconds, 1, int.MaxValue), cancellationToken))
            {
                continue;
            }

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                var data = doc.RootElement;
                if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("id", out var id))
                {
                    continue;
                }

                var matches = id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var n) && n == expected
                    || id.ValueKind == JsonValueKind.String && id.GetString() == expected.ToString();
                if (matches)
                {
                    return data.Clone();
                }
            }
        }

        throw new FetchException("codex app-server の応答がありません");
    }

    private static JsonElement ExtractRateLimits(JsonElement payload)
    {
        if (payload.TryGetProperty("rateLimits", out var rateLimits) && rateLimits.ValueKind == JsonValueKind.Object)
        {
            return rateLimits;
        }

        if (payload.TryGetProperty("rate_limits", out var rateLimitsSnake) && rateLimitsSnake.ValueKind == JsonValueKind.Object)
        {
            return rateLimitsSnake;
        }

        if (payload.TryGetProperty("rate_limit", out var rateLimit) && rateLimit.ValueKind == JsonValueKind.Object)
        {
            return rateLimit;
        }

        if (payload.TryGetProperty("rateLimitsByLimitId", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            if (nested.TryGetProperty("codex", out var codex) && codex.ValueKind == JsonValueKind.Object)
            {
                return codex;
            }

            foreach (var item in nested.EnumerateObject())
            {
                if (item.Name.Equals("chatgpt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.Value.ValueKind == JsonValueKind.Object)
                {
                    return item.Value;
                }
            }
        }

        if (payload.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Object)
        {
            return limits;
        }

        return payload;
    }

    private static List<JsonElement> SourceDicts(JsonElement payload)
    {
        var sources = new List<JsonElement> { payload, ExtractRateLimits(payload) };
        if (payload.TryGetProperty("limits", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            sources.Add(extra);
        }

        foreach (var source in sources.ToList())
        {
            if (source.TryGetProperty("codex", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                sources.Add(nested);
            }
        }

        return sources;
    }

    private static RateWindow? WindowFrom(JsonElement data, string defaultLabel)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? usedPercent = Formatting.Number(data, "usedPercent") ?? Formatting.Number(data, "used_percent");
        if (usedPercent is null)
        {
            var utilization = Formatting.Number(data, "utilization");
            if (utilization is not null)
            {
                usedPercent = utilization <= 1.5 ? utilization * 100.0 : utilization;
            }
        }

        var remaining = Formatting.Number(data, "remainingPercent") ?? Formatting.Number(data, "remaining_percent");
        if (usedPercent is null && remaining is not null)
        {
            var rem = remaining.Value <= 1.5 ? remaining.Value * 100.0 : remaining.Value;
            usedPercent = Math.Max(0.0, 100.0 - rem);
        }

        var duration = Formatting.Number(data, "windowDurationMins")
            ?? Formatting.Number(data, "window_duration_mins")
            ?? Formatting.Number(data, "windowMinutes")
            ?? Formatting.Number(data, "window_minutes");
        var windowSeconds = Formatting.Number(data, "limit_window_seconds") ?? Formatting.Number(data, "limitWindowSeconds");
        if (duration is null && windowSeconds is not null && windowSeconds.Value > 0)
        {
            duration = Math.Floor((windowSeconds.Value + 59) / 60.0);
        }

        int? durationMins = duration is null ? null : (int)Math.Round(duration.Value);
        var label = durationMins is null ? defaultLabel : LabelForDuration(durationMins.Value, defaultLabel);
        DateTimeOffset? resets = null;
        if (data.TryGetProperty("resetsAt", out var resetsAt))
        {
            resets = Formatting.ParseTimestamp(resetsAt);
        }
        else if (data.TryGetProperty("resets_at", out var resetsSnake))
        {
            resets = Formatting.ParseTimestamp(resetsSnake);
        }
        else if (data.TryGetProperty("reset_at", out var resetAt))
        {
            resets = Formatting.ParseTimestamp(resetAt);
        }
        else if (data.TryGetProperty("resetAt", out var resetAtCamel))
        {
            resets = Formatting.ParseTimestamp(resetAtCamel);
        }

        if (usedPercent is null && resets is null)
        {
            return null;
        }

        return new RateWindow(label, usedPercent, resets, durationMins);
    }

    internal static string LabelForDuration(int durationMins, string defaultLabel)
    {
        if (IsApproximateWindow(durationMins, 5 * 60))
        {
            return "5h";
        }

        if (IsApproximateWindow(durationMins, 24 * 60))
        {
            return "1d";
        }

        if (IsApproximateWindow(durationMins, 7 * 24 * 60))
        {
            return "7d";
        }

        if (IsApproximateWindow(durationMins, 30 * 24 * 60))
        {
            return "30d";
        }

        if (IsApproximateWindow(durationMins, 365 * 24 * 60))
        {
            return "1y";
        }

        return defaultLabel;
    }

    private static bool IsApproximateWindow(int minutes, int expectedMinutes)
    {
        var value = (double)minutes;
        var expected = (double)expectedMinutes;
        return value >= expected * 0.95 && value <= expected * 1.05;
    }

    private static string? CreditLine(JsonElement credits)
    {
        if (credits.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (credits.TryGetProperty("unlimited", out var unlimited) && unlimited.ValueKind == JsonValueKind.True)
        {
            return "Credits  無制限";
        }

        if (!credits.TryGetProperty("hasCredits", out var has) || has.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        if (!credits.TryGetProperty("balance", out var balance))
        {
            return "Credits  あり";
        }

        var text = balance.ValueKind switch
        {
            JsonValueKind.String => balance.GetString(),
            JsonValueKind.Number => balance.ToString(),
            _ => null,
        };
        if (string.IsNullOrEmpty(text) || text == "0")
        {
            return "Credits  あり";
        }

        return $"Credits  {text}";
    }

    private static string? SafeHttpError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var trimmed = body.Trim();
        if (trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("<!doctype", StringComparison.OrdinalIgnoreCase))
        {
            return "html";
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var message = Formatting.PickStr(doc.RootElement, "message", "detail", "error", "code");
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                message = Formatting.PickStr(error, "message", "code", "type") ?? message;
            }

            return SafeErrorToken(message);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SafeErrorToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.Contains("eyJ", StringComparison.Ordinal) || value.Count(ch => ch == '.') >= 2)
        {
            return null;
        }

        return value.Length > 80 ? value[..77] + "..." : value;
    }

    private static JsonElement ObjectOrEmpty(JsonElement parent, string key) =>
        Formatting.Object(parent, key) ?? default;
}
