using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core.Providers;

public static class OpenCodeProvider
{
    /// <summary>
    /// opencode の console API。使用量は console のログインセッション（Cookie）でのみ読める。
    /// API キーでは 403 が返るため、キーしか無い場合は「要認証」にする。
    /// </summary>
    public const string DefaultUsageUrl = "https://opencode.ai/console/api/usage/summary";

    /// <summary>ログイン中のアカウントが属するワークスペース一覧。</summary>
    public const string DefaultWorkspacesUrl = "https://opencode.ai/console/api/user/workspaces";

    /// <summary>go/status はこのヘッダでワークスペースを選ぶ。</summary>
    public const string WorkspaceHeader = "x-org-id";

    public const string UserAgent = "ai-usage-bar";

    private static readonly Regex WorkspacePattern = new(@"(?:wrk|org)_[A-Za-z0-9]+", RegexOptions.Compiled);

    private static readonly (string Key, string Label, int DurationMins)[] Aliases =
    [
        ("five_hour", "5h", 5 * 60),
        ("fiveHour", "5h", 5 * 60),
        ("5h", "5h", 5 * 60),
        ("hourly", "5h", 5 * 60),
        ("seven_day", "7d", 7 * 24 * 60),
        ("sevenDay", "7d", 7 * 24 * 60),
        ("7d", "7d", 7 * 24 * 60),
        ("weekly", "7d", 7 * 24 * 60),
        ("monthly", "30d", 30 * 24 * 60),
        ("30d", "30d", 30 * 24 * 60),
    ];

    public static async Task<ProviderSnapshot> FetchAsync(HttpClient http, ProviderOptions options, CancellationToken cancellationToken)
    {
        var updated = Formatting.UtcNow();
        try
        {
            var cookie = NormalizeCookie(CredentialStore.GetToken(CredentialUsers.OpenCodeCookie));
            var session = await ResolveSessionAsync(http, cancellationToken).ConfigureAwait(false);
            var apiKey = CredentialStore.GetToken(CredentialUsers.OpenCode)
                ?? Environment.GetEnvironmentVariable(OpenCodeAuth.EnvVar)
                ?? OpenCodeAuth.LoadApiKey();
            if (cookie is null && session is null && apiKey is null)
            {
                AppLog.Info("OpenCode のコンソール セッションも Cookie も API キーも見つかりません");
                throw new AuthRequiredException("OpenCode の認証情報が見つかりません");
            }

            var token = session?.AccessToken ?? apiKey;
            var workspaces = ResolveWorkspaces(options, session, token).ToList();
            var limits = await TryFetchGoLimitsAsync(http, options, cookie, token, workspaces, cancellationToken).ConfigureAwait(false);
            if (limits.Payload is not { } firstLimits || CollectGoMeters(firstLimits).Count == 0)
            {
                // セッションの workspace が Go 非対応・古い場合に備え、一覧から候補を追加する。
                var discovered = await TryFetchWorkspacesAsync(http, options, cookie, token, cancellationToken).ConfigureAwait(false);
                var extra = discovered
                    .Where(id => !workspaces.Contains(id, StringComparer.Ordinal))
                    .ToList();
                if (extra.Count > 0)
                {
                    AppLog.Info($"OpenCode のワークスペースを追加で試します {extra.Count} 件");
                    workspaces.AddRange(extra);
                    var retry = await TryFetchGoLimitsAsync(http, options, cookie, token, extra, cancellationToken).ConfigureAwait(false);
                    if (retry.Payload is not null)
                    {
                        limits = retry;
                    }
                }
            }

            var workspace = limits.Payload is not null ? limits.Workspace : workspaces.FirstOrDefault();
            // Limits が読めたら summary は取りに行かない。取れないときだけ従来の集計 API に戻す。
            var payload = limits.Payload is { } limitsPayload && CollectGoMeters(limitsPayload).Count > 0
                ? limitsPayload
                : await SendWithWorkspaceFallbackAsync(http, UsageUrl(options), cookie, token, workspace, cancellationToken).ConfigureAwait(false);
            var snapshot = FromPayload(payload, limits.Payload, updated);
            if (snapshot.Ok)
            {
                AppLog.Info("OpenCode の使用量を取得しました");
            }

            return snapshot;
        }
        catch (AuthRequiredException ex)
        {
            AppLog.Info($"OpenCode の認証が必要です: {CredentialStore.AsSafeError(ex)}");
            return new ProviderSnapshot("opencode", "OpenCode Go", false, UiText.CompactError("OpenCode Go", ProviderErrors.AuthRequired), ["OpenCode Go", UiText.OpenCodeAuthHint], ProviderErrors.AuthRequired, updated);
        }
        catch (RateLimitedException)
        {
            return new ProviderSnapshot("opencode", "OpenCode Go", false, UiText.CompactError("OpenCode Go", ProviderErrors.RateLimited), ["OpenCode Go", UiText.RateLimitedMessage], ProviderErrors.RateLimited, updated);
        }
        catch (EndpointMissingException)
        {
            AppLog.Info($"OpenCode の使用量 URL が未対応です: {UsageUrl(options)}");
            return new ProviderSnapshot("opencode", "OpenCode Go", false, UiText.CompactError("OpenCode Go", ProviderErrors.EndpointMissing), ["OpenCode Go", UiText.OpenCodeEndpointHint], ProviderErrors.EndpointMissing, updated);
        }
        catch (FetchException ex)
        {
            AppLog.Error("OpenCode の HTTP 取得に失敗しました", ex);
            return new ProviderSnapshot("opencode", "OpenCode Go", false, "OpenCode Go --", ["OpenCode Go", UiText.OpenCodeFetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
        catch (Exception ex)
        {
            AppLog.Error("OpenCode の使用量取得で予期しないエラー", ex);
            return new ProviderSnapshot("opencode", "OpenCode Go", false, "OpenCode Go --", ["OpenCode Go", UiText.OpenCodeFetchFailedMessage], ProviderErrors.FetchFailed, updated);
        }
    }

    public static string UsageUrl(ProviderOptions options) =>
        string.IsNullOrWhiteSpace(options.OpenCodeUsageUrl) ? DefaultUsageUrl : options.OpenCodeUsageUrl.Trim();

    /// <summary>
    /// 設定値にはワークスペース ID でも console の URL でも受け付ける。
    /// 例: wrk_... / https://opencode.ai/console/wrk_.../go
    /// </summary>
    public static string? NormalizeWorkspaceId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Trim('"', '\'');
        var match = WorkspacePattern.Match(text);
        return match.Success ? match.Value : null;
    }

    internal static bool IsMissingEndpoint(int status) => status is 404 or 405;

    /// <summary>console の Limits パネル（Rolling / Weekly / Monthly）。</summary>
    public static string? GoLimitsUrl(ProviderOptions options)
    {
        var url = UsageUrl(options);
        const string suffix = "/usage/summary";
        if (!url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return url[..^suffix.Length] + "/go/status";
    }

    /// <summary>go/status の x-org-id を決めるためのワークスペース一覧。</summary>
    public static string? WorkspacesUrl(ProviderOptions options)
    {
        var url = UsageUrl(options);
        const string suffix = "/usage/summary";
        if (!url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return url[..^suffix.Length] + "/user/workspaces";
    }

    public static IReadOnlyList<RateWindow> CollectGoMeters(JsonElement payload)
    {
        // /api/go/status は access.meters に入っている（古い形では直下の meters）。
        var meters = Formatting.Object(payload, "meters");
        if (meters is null && Formatting.Object(payload, "access") is { } access)
        {
            meters = Formatting.Object(access, "meters");
        }

        if (meters is not { } source)
        {
            return [];
        }

        var windows = new List<RateWindow>();
        foreach (var key in new[] { "fiveHour", "five_hour", "week", "weekly", "month", "monthly" })
        {
            if (!source.TryGetProperty(key, out var meter) || meter.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var label = Formatting.PickStr(meter, "label") ?? DefaultMeterLabel(key);
            if (windows.Any(window => string.Equals(window.Label, label, StringComparison.Ordinal)))
            {
                continue;
            }

            var used = Formatting.NumberOrString(meter, "usedMicroCents", "used_micro_cents");
            var limit = Formatting.NumberOrString(meter, "limitMicroCents", "limit_micro_cents");
            var percent = used is not null && limit is > 0
                ? Math.Clamp(used.Value / limit.Value * 100.0, 0, 100)
                : WindowReader.UsedPercent(meter);
            var resets = WindowReader.ResetsAt(meter);
            if (percent is null && resets is null)
            {
                continue;
            }

            windows.Add(new RateWindow(label, percent, resets));
        }

        return windows;
    }

    private static string DefaultMeterLabel(string key) => key switch
    {
        "fiveHour" or "five_hour" => "5h",
        "week" or "weekly" => "Weekly",
        _ => "Monthly",
    };

    private static bool IsRollingLabel(string label) =>
        label is "Rolling" or "5h";

    private sealed record GoLimitsResult(JsonElement? Payload, string? Workspace);

    /// <summary>
    /// 設定値 → セッションの org_id → アクセストークンの claim、の順で候補を作る。
    /// 候補で Go limits が読めないときだけ、後段でワークスペース一覧を取りに行く。
    /// </summary>
    private static IReadOnlyList<string> ResolveWorkspaces(ProviderOptions options, OpenCodeSession? session, string? token)
    {
        var workspaces = new List<string>();
        AddWorkspace(workspaces, options.OpenCodeWorkspaceId);
        AddWorkspace(workspaces, session?.WorkspaceId);
        AddWorkspace(workspaces, WorkspaceFromToken(token));
        if (workspaces.Count > 0)
        {
            AppLog.Info($"OpenCode のワークスペース候補 {workspaces.Count} 件");
        }

        return workspaces;
    }

    private static void AddWorkspace(ICollection<string> workspaces, string? raw)
    {
        var workspace = NormalizeWorkspaceId(raw);
        if (workspace is not null && !workspaces.Contains(workspace, StringComparer.Ordinal))
        {
            workspaces.Add(workspace);
        }
    }

    private static string? WorkspaceFromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var claims = Formatting.JwtPayload(token);
        return NormalizeWorkspaceId(Formatting.PickStr(
            claims,
            "org_id",
            "orgId",
            "workspace_id",
            "workspaceId",
            "org",
            "workspace"));
    }

    private static async Task<IReadOnlyList<string>> TryFetchWorkspacesAsync(
        HttpClient http,
        ProviderOptions options,
        string? cookie,
        string? token,
        CancellationToken cancellationToken)
    {
        var url = WorkspacesUrl(options);
        if (url is null)
        {
            return [];
        }

        try
        {
            var payload = await SendAsync(http, url, cookie, token, null, cancellationToken).ConfigureAwait(false);
            var workspaces = CollectWorkspaceIds(payload);
            if (workspaces.Count == 0)
            {
                AppLog.Info($"OpenCode のワークスペース一覧に ID がありません keys={KeyNames(payload)}");
            }

            return workspaces;
        }
        catch (Exception ex) when (ex is FetchException or AuthRequiredException or RateLimitedException or EndpointMissingException)
        {
            AppLog.Info($"OpenCode のワークスペース一覧は取得できませんでした: {ex.Message}");
            return [];
        }
    }

    /// <summary>ワークスペース一覧から wrk_ / org_ の ID を拾う。</summary>
    public static IReadOnlyList<string> CollectWorkspaceIds(JsonElement payload)
    {
        IEnumerable<JsonElement> items = payload.ValueKind == JsonValueKind.Array
            ? payload.EnumerateArray()
            : Array.Empty<JsonElement>();
        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "workspaces", "orgs", "data", "items", "results" })
            {
                if (payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                {
                    items = value.EnumerateArray();
                    break;
                }
            }

            if (!items.Any() && payload.TryGetProperty("id", out _))
            {
                items = [payload];
            }
        }

        var workspaces = new List<string>();
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("blocked", out var blocked)
                && blocked.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            AddWorkspace(workspaces, Formatting.PickStr(item, "id", "workspaceId", "workspace_id", "orgId", "org_id"));
        }

        return workspaces;
    }

    private static async Task<GoLimitsResult> TryFetchGoLimitsAsync(
        HttpClient http,
        ProviderOptions options,
        string? cookie,
        string? token,
        IReadOnlyList<string> workspaces,
        CancellationToken cancellationToken)
    {
        var url = GoLimitsUrl(options);
        if (url is null)
        {
            return new GoLimitsResult(null, null);
        }

        // 候補ごとに試し、Limits（access.meters）が読めたワークスペースを採用する。
        // ヘッダ無しでも読める場合があるので、最後にヘッダ無しも試す。
        var candidates = workspaces.Count > 0
            ? workspaces.Cast<string?>().Append(null)
            : new string?[] { null };
        JsonElement? fallback = null;
        string? fallbackWorkspace = null;
        foreach (var workspace in candidates)
        {
            try
            {
                var payload = await SendAsync(http, url, cookie, token, workspace, cancellationToken).ConfigureAwait(false);
                if (CollectGoMeters(payload).Count > 0)
                {
                    AppLog.Info($"OpenCode の Limits を取得しました workspace={workspace ?? "(default)"}");
                    return new GoLimitsResult(payload, workspace);
                }

                fallback ??= payload;
                fallbackWorkspace ??= workspace;
            }
            catch (RateLimitedException)
            {
                // 429 のあとに別 workspace を叩くと悪化するので、ここで打ち切る。
                throw;
            }
            catch (Exception ex) when (ex is FetchException or AuthRequiredException or EndpointMissingException)
            {
                AppLog.Info($"OpenCode の Limits は取得できませんでした workspace={workspace ?? "(default)"}: {ex.Message}");
            }
        }

        return new GoLimitsResult(fallback, fallbackWorkspace);
    }

    /// <summary>
    /// console にサインイン済みならその access_token を使う。期限が近ければ更新する。
    /// </summary>
    private static async Task<OpenCodeSession?> ResolveSessionAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var session = OpenCodeAuth.LoadSession();
        if (session is null)
        {
            return null;
        }

        if (OpenCodeAuth.NeedsRefresh(session) && session.RefreshToken is { } refresh)
        {
            var refreshed = await OpenCodeAuth.RefreshAsync(http, refresh, cancellationToken).ConfigureAwait(false);
            if (refreshed is not null)
            {
                // 更新応答に org_id が無いことがあるので、分かっているワークスペースは残す。
                refreshed = refreshed.WorkspaceId is null && session.WorkspaceId is not null
                    ? refreshed with { WorkspaceId = session.WorkspaceId }
                    : refreshed;
                OpenCodeAuth.SaveSession(refreshed);
                return refreshed;
            }

            AppLog.Info("OpenCode のセッション更新に失敗したので保存済みトークンで試します");
        }

        return session;
    }

    /// <summary>
    /// 貼り付けられた値から Cookie ヘッダに載せる部分を作る。
    /// "Cookie: a=1; b=2" でも "a=1; b=2" でも受ける。
    /// </summary>
    internal static string? NormalizeCookie(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Trim('"');
        if (text.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Cookie:".Length..].Trim();
        }

        if (text.Any(ch => ch is '\r' or '\n'))
        {
            AppLog.Warning("OpenCode の Cookie に改行が含まれているため使いません");
            return null;
        }

        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static IReadOnlyList<RateWindow> CollectWindows(JsonElement payload)
    {
        var windows = new List<RateWindow>();
        foreach (var container in WindowReader.Containers(payload, "limits", "windows", "rate_limits", "usage", "data", "plan"))
        {
            foreach (var (key, label, duration) in Aliases)
            {
                if (!container.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object)
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

    public static ProviderSnapshot FromPayload(JsonElement payload, DateTimeOffset updated) =>
        FromPayload(payload, null, updated);

    public static ProviderSnapshot FromPayload(JsonElement payload, JsonElement? limits, DateTimeOffset updated)
    {
        var root = Unwrap(payload);
        var meters = limits is { } limitsPayload ? CollectGoMeters(limitsPayload) : [];
        var windows = meters.Count > 0 ? meters : CollectWindows(root);
        if (meters.Count > 0)
        {
            AppLog.Info("OpenCode の Limits: " + string.Join(
                " / ",
                meters.Select(meter => $"{meter.Label} {Formatting.Percent(meter.UsedPercent ?? 0)}")));
        }
        else if (limits is { } rawLimits)
        {
            AppLog.Info($"OpenCode の Limits 応答が読めません keys={Describe(rawLimits)}");
        }
        var budget = Budget(root);
        var balance = Balance(root);
        var usage = Summary(root);
        var plan = Formatting.PickStr(root, "plan", "planId", "plan_id", "tier", "subscription")
            ?? PlanName(Formatting.PickStr(root, "product"));
        if (plan is null && limits is { } limitRoot)
        {
            plan = Formatting.PickStr(limitRoot, "plan", "planId", "plan_id", "tier", "subscription")
                ?? PlanName(Formatting.PickStr(limitRoot, "product"));
        }

        var compactParts = new List<string> { "OpenCode Go" };
        if (windows.Count > 0)
        {
            compactParts.Add(string.Join(" · ", windows.Select(item => item.Compact())));
        }
        else if (budget is not null)
        {
            compactParts.Add(Formatting.Percent(budget.Value));
        }
        else if (balance is not null)
        {
            compactParts.Add(MoneyText(balance.Value));
        }
        else if (usage is { Cost: not null })
        {
            compactParts.Add(MoneyText(usage.Cost.Value));
        }
        else if (usage is { Requests: not null })
        {
            compactParts.Add($"{UiText.CompactCount(usage.Requests.Value)} req");
        }
        else
        {
            compactParts.Add("--");
        }

        var lines = new List<string>();
        if (budget is not null)
        {
            lines.Add(UiText.Labeled(UiText.Plan, Formatting.Percent(budget.Value)));
        }

        if (balance is not null)
        {
            lines.Add(UiText.Labeled(UiText.Credits, MoneyText(balance.Value)));
        }

        lines.Add(UiText.Labeled(UiText.LastUpdated, updated.ToLocalTime().ToString("HH:mm:ss")));
        var metrics = windows.Select(window =>
        {
            var hint = window.ResetsAt is null
                ? (IsRollingLabel(window.Label) ? UiText.StartsOnFirstUse : null)
                : UiText.ResetHint(Formatting.FormatReset(window.ResetsAt, updated));
            return new UsageMetric(window.Label, window.UsedPercent, hint);
        }).ToList();

        // バーは 5時間（Rolling）の使用率を出す。無い場合だけ先頭の枠に戻す。
        var primaryWindow = windows.FirstOrDefault(window => IsRollingLabel(window.Label) && window.UsedPercent is not null)
            ?? windows.FirstOrDefault(window => window.UsedPercent is not null);
        var ok = windows.Count > 0 || budget is not null || balance is not null || usage is not null;
        if (!ok)
        {
            AppLog.Info($"OpenCode の応答に既知の項目がありません keys={KeyNames(root)}");
        }

        return new ProviderSnapshot(
            "opencode",
            "OpenCode Go",
            ok,
            string.Join(" ", compactParts),
            lines,
            ok ? null : ProviderErrors.Unparsed,
            updated,
            primaryWindow?.UsedPercent ?? budget,
            plan,
            metrics);
    }

    private static string? PlanName(string? product) => product switch
    {
        null or "" => null,
        "go" => "Go",
        "go-plus" => "Go Plus",
        "go-teams" => "Go Teams",
        _ => product,
    };

    private sealed record UsageSummary(double? Requests, double? InputTokens, double? OutputTokens, double? CacheTokens, double? Cost);

    /// <summary>
    /// /console/api/usage/summary の集計値。残量ではなく期間内の実績。
    /// </summary>
    private static UsageSummary? Summary(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var requests = Formatting.NumberOrString(root, "totalRequests", "total_requests", "requests");
        var input = Formatting.NumberOrString(root, "totalInputTokens", "total_input_tokens", "inputTokens");
        var output = Formatting.NumberOrString(root, "totalOutputTokens", "total_output_tokens", "outputTokens");
        var cacheRead = Formatting.NumberOrString(root, "totalCacheReadTokens", "total_cache_read_tokens") ?? 0;
        var cacheWrite = (Formatting.NumberOrString(root, "totalCacheWrite5mTokens", "total_cache_write_5m_tokens") ?? 0)
            + (Formatting.NumberOrString(root, "totalCacheWrite1hTokens", "total_cache_write_1h_tokens") ?? 0);
        var costMicro = Formatting.NumberOrString(root, "totalCostMicroCents", "total_cost_micro_cents");
        var cost = costMicro is not null
            ? costMicro.Value / 100_000_000.0
            : Formatting.NumberOrString(root, "totalCost", "total_cost", "totalUsed", "total_used");
        var cache = cacheRead + cacheWrite;
        if (requests is null && input is null && output is null && cost is null)
        {
            return null;
        }

        return new UsageSummary(requests, input, output, cache, cost);
    }

    /// <summary>応答の形を突き止めるための、キー名だけの要約（値は出さない）。</summary>
    private static string Describe(JsonElement element, int depth = 0)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var first = element.EnumerateArray().FirstOrDefault();
            return $"[{Describe(first, depth)}]";
        }

        if (element.ValueKind != JsonValueKind.Object || depth > 2)
        {
            return element.ValueKind.ToString().ToLowerInvariant();
        }

        return string.Join(
            ",",
            element.EnumerateObject().Take(24).Select(property => $"{property.Name}={Describe(property.Value, depth + 1)}"));
    }

    private static string KeyNames(JsonElement root)    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            var first = root.EnumerateArray().FirstOrDefault();
            return first.ValueKind == JsonValueKind.Object ? $"[{KeyNames(first)}]" : $"[{first.ValueKind}]";
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return root.ValueKind.ToString();
        }

        return string.Join(",", root.EnumerateObject().Select(property => property.Name).Take(16));
    }

    private static JsonElement Unwrap(JsonElement payload)
    {
        var root = payload;
        foreach (var key in new[] { "data", "usage", "account" })
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(key, out var nested)
                && nested.ValueKind == JsonValueKind.Object
                && !root.TryGetProperty("balance", out _)
                && !root.TryGetProperty("five_hour", out _))
            {
                root = nested;
            }
        }

        return root;
    }

    private static double? Balance(JsonElement root)
    {
        var direct = Number(root, "balance", "credit_balance", "credits_remaining", "remainingBalance");
        if (direct is not null)
        {
            return direct;
        }

        if (Formatting.Object(root, "credits") is { } credits)
        {
            return Money(credits, "balance", "remaining", "available");
        }

        return null;
    }

    /// <summary>
    /// 予算オブジェクト（limitMicroCents / spentMicroCents）から使用率を出す。
    /// </summary>
    private static double? Budget(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "budget", "budgets", "orgBudget", "defaultBudget" })
        {
            if (Formatting.Object(root, name) is { } budget && BudgetPercent(budget) is { } percent)
            {
                return percent;
            }
        }

        return BudgetPercent(root);
    }

    private static double? BudgetPercent(JsonElement obj)
    {
        var limit = Number(obj, "limitMicroCents", "limit_micro_cents");
        var spent = Number(obj, "spentMicroCents", "spent_micro_cents", "usedMicroCents");
        if (limit is > 0 && spent is not null)
        {
            return Math.Clamp(spent.Value / limit.Value * 100.0, 0, 100);
        }

        return null;
    }

    private static double? Money(JsonElement obj, params string[] keys)
    {
        var micro = MoneyMicro(obj, "spentMicroCents", "totalCostMicroCents", "costMicroCents");
        if (micro is not null)
        {
            return micro;
        }

        return Number(obj, keys);
    }

    private static double? MoneyMicro(JsonElement obj, params string[] keys)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (Formatting.Number(obj, key) is { } value)
            {
                return value / 100_000_000.0;
            }
        }

        return null;
    }

    private static double? Number(JsonElement obj, params string[] keys)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            var value = Formatting.Number(obj, key);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string MoneyText(double value) => "$" + value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// 古いセッションでは x-org-id が無いほうが通ることがある。
    /// ワークスペース付きで失敗したらヘッダ無しでもう一度試す。
    /// </summary>
    private static async Task<JsonElement> SendWithWorkspaceFallbackAsync(
        HttpClient http,
        string url,
        string? cookie,
        string? apiKey,
        string? workspace,
        CancellationToken cancellationToken)
    {
        if (workspace is null)
        {
            return await SendAsync(http, url, cookie, apiKey, null, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await SendAsync(http, url, cookie, apiKey, workspace, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AuthRequiredException or FetchException or EndpointMissingException)
        {
            AppLog.Info($"OpenCode の取得に x-org-id={workspace} で失敗したため、指定なしで再試行します: {ex.Message}");
            return await SendAsync(http, url, cookie, apiKey, null, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<JsonElement> SendAsync(
        HttpClient http,
        string url,
        string? cookie,
        string? apiKey,
        string? workspace,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (apiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (workspace is not null)
        {
            request.Headers.TryAddWithoutValidation(WorkspaceHeader, workspace);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            throw new FetchException("OpenCode の HTTP 取得に失敗しました", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403)
            {
                AppLog.Info($"OpenCode HTTP status={status} (cookie={(cookie is null ? "no" : "yes")})");
                throw new AuthRequiredException("OpenCode の console セッションが無効です");
            }

            if (status == 429)
            {
                throw new RateLimitedException();
            }

            if (IsMissingEndpoint(status))
            {
                throw new EndpointMissingException($"OpenCode HTTP status={status}");
            }

            if (status >= 400)
            {
                throw new FetchException($"OpenCode HTTP status={status}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (JsonException ex)
            {
                throw new FetchException("OpenCode の応答が JSON ではありません", ex);
            }

            var payload = doc.RootElement.Clone();
            doc.Dispose();
            if (payload.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                throw new FetchException("OpenCode の応答形式が想定と違います");
            }

            return payload;
        }
    }
}
