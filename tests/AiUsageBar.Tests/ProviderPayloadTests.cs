using System.Text.Json;
using AiUsageBar.Core;
using AiUsageBar.Core.Auth;
using AiUsageBar.Core.Providers;

namespace AiUsageBar.Tests;

public class ClaudePayloadTests
{
    private const string Payload = """
        {
          "five_hour": { "utilization": 24, "resets_at": "2026-10-22T12:00:00Z" },
          "seven_day": { "utilization": 46, "resets_at": "2026-10-25T00:00:00Z" },
          "seven_day_opus": { "utilization": 12, "resets_at": "2026-10-25T00:00:00Z" }
        }
        """;

    [Fact]
    public void CollectsFiveHourSevenDayAndOpus()
    {
        using var doc = JsonDocument.Parse(Payload);
        var windows = ClaudeProvider.CollectWindows(doc.RootElement);
        Assert.Equal(3, windows.Count);
        Assert.Equal("5h", windows[0].Label);
        Assert.Equal(24, windows[0].UsedPercent);
        Assert.Equal("7d", windows[1].Label);
        Assert.Equal("Opus", windows[2].Label);
    }

    [Fact]
    public void CompactShowsRemainingForEachWindow()
    {
        using var doc = JsonDocument.Parse(Payload);
        using var _ = new UiCulture("ja-JP");
        var snapshot = ClaudeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Claude 5h 76% · 7d 54% · Opus 88%", snapshot.Compact);
        Assert.Equal(46, snapshot.UsedPercent);
        Assert.Equal("46%", Formatting.ShortMetric(snapshot));
        Assert.Contains("5h  残り 76%", snapshot.Lines[1]);
        Assert.Equal("5h", snapshot.MetricList[0].Label);
        Assert.Contains("残り 76%", snapshot.MetricList[0].Hint);
    }

    [Fact]
    public void EmptyPayloadIsUnparsed()
    {
        using var doc = JsonDocument.Parse("""{ "error": "nope" }""");
        var snapshot = ClaudeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.False(snapshot.Ok);
        Assert.Equal(ProviderErrors.Unparsed, snapshot.Error);
        Assert.Equal("Claude --", snapshot.Compact);
    }
}

public class CommandCodePayloadTests
{
    private const string Payload = """
        {
          "credits": {
            "planId": "goat",
            "monthlyCredits": 8.4,
            "purchasedCredits": 2,
            "freeCredits": 0,
            "windowLimits": {
              "fiveHour": { "used": 3, "cap": 20, "resetAt": "2026-10-22T12:00:00Z" },
              "weekly": { "used": 12, "cap": 60, "resetAt": "2026-10-25T00:00:00Z" }
            }
          }
        }
        """;

    [Fact]
    public void ReadsWindowLimitsAndCredits()
    {
        using var doc = JsonDocument.Parse(Payload);
        using var _ = new UiCulture("ja-JP");
        var snapshot = CommandCodeProvider.FromPayload(doc.RootElement, null, DateTimeOffset.UnixEpoch);
        Assert.Equal("Command Code 5h 85% · 7d 80%", snapshot.Compact);
        Assert.Equal(20, snapshot.UsedPercent);
        Assert.Equal("goat", snapshot.Subtitle);
        Assert.Contains("Command Code  goat", snapshot.Lines);
        Assert.Contains("Credits  $10.40", snapshot.Lines);
        Assert.Equal(2, snapshot.MetricList.Count);
        Assert.Equal("5h", snapshot.MetricList[0].Label);
    }

    [Fact]
    public void FallsBackToCreditsWhenNoWindowLimits()
    {
        using var doc = JsonDocument.Parse("""{ "credits": { "monthlyCredits": 3.5 } }""");
        var snapshot = CommandCodeProvider.FromPayload(doc.RootElement, null, DateTimeOffset.UnixEpoch);
        Assert.Equal("Command Code $3.50", snapshot.Compact);
        Assert.Null(snapshot.UsedPercent);
        Assert.Equal("$3.50", Formatting.ShortMetric(snapshot));
        Assert.True(snapshot.Ok);
    }

    [Fact]
    public void UnknownShapeIsUnparsed()
    {
        using var doc = JsonDocument.Parse("""{ "credits": { } }""");
        var snapshot = CommandCodeProvider.FromPayload(doc.RootElement, null, DateTimeOffset.UnixEpoch);
        Assert.False(snapshot.Ok);
        Assert.Equal(ProviderErrors.Unparsed, snapshot.Error);
    }
}

public class OpenCodePayloadTests
{
    [Fact]
    public void BalanceOnlyShowsMoney()
    {
        using var doc = JsonDocument.Parse("""{ "balance": 12.34, "total_used": 41.2 }""");
        using var _ = new UiCulture("ja-JP");
        var snapshot = OpenCodeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("OpenCode Go $12.34", snapshot.Compact);
        Assert.Equal("$12.34", Formatting.ShortMetric(snapshot));
        Assert.Contains("Credits  $12.34", snapshot.Lines);
        Assert.Null(snapshot.UsedPercent);
    }

    [Fact]
    public void ReadsWindowsWhenPresent()
    {
        const string json = """
            {
              "limits": {
                "five_hour": { "used_percent": 30, "resets_at": "2026-10-22T12:00:00Z" },
                "weekly": { "used": 6, "cap": 60 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = OpenCodeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("OpenCode Go 5h 70% · 7d 90%", snapshot.Compact);
        Assert.Equal(30, snapshot.UsedPercent);
        Assert.Equal(2, snapshot.MetricList.Count);
    }

    [Fact]
    public void ReadsConsoleUsageSummary()
    {
        const string json = """
            {
              "totalRequests": "1234",
              "totalInputTokens": 12345678,
              "totalOutputTokens": 456789,
              "totalCacheReadTokens": 1000000,
              "totalCacheWrite5mTokens": 200000,
              "totalCacheWrite1hTokens": 300000,
              "totalCostMicroCents": "1234567890",
              "services": []
            }
            """;
        using var doc = JsonDocument.Parse(json);
        using var _ = new UiCulture("ja-JP");
        var snapshot = OpenCodeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.True(snapshot.Ok);
        Assert.Equal("OpenCode Go $12.35", snapshot.Compact);
        Assert.Equal("$12.35", Formatting.ShortMetric(snapshot));
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("リクエスト"));
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("トークン"));
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("キャッシュ"));
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("コスト"));
        Assert.Null(snapshot.UsedPercent);
    }

    [Fact]
    public void ConsoleSummaryWithoutCostFallsBackToRequests()
    {
        using var doc = JsonDocument.Parse("""{ "totalRequests": 42 }""");
        var snapshot = OpenCodeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.True(snapshot.Ok);
        Assert.Equal("OpenCode Go 42 req", snapshot.Compact);
    }

    [Fact]
    public void ReadsGoLimitsMeters()
    {
        const string json = """
            {
              "product": "go",
              "access": {
                "startsAt": "2026-10-24T00:00:00Z",
                "meters": {
                  "fiveHour": { "usedMicroCents": 0, "limitMicroCents": 1000000000, "resetsAt": null },
                  "week": { "usedMicroCents": "230000000", "limitMicroCents": "1000000000", "resetsAt": "2026-10-25T20:00:00Z" },
                  "month": { "usedMicroCents": 750000000, "limitMicroCents": 1000000000, "resetsAt": "2026-11-10T06:00:00Z" }
                }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        using var _ = new UiCulture("ja-JP");
        var meters = OpenCodeProvider.CollectGoMeters(doc.RootElement);
        Assert.Equal(3, meters.Count);
        Assert.Equal("5h", meters[0].Label);
        Assert.Equal(0, meters[0].UsedPercent);
        Assert.Null(meters[0].ResetsAt);
        Assert.Equal(23, meters[1].UsedPercent);
        Assert.Equal(75, meters[2].UsedPercent);

        using var summaryDoc = JsonDocument.Parse("""{ "totalCostMicroCents": "1234567890" }""");
        var snapshot = OpenCodeProvider.FromPayload(summaryDoc.RootElement, doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal(0, snapshot.UsedPercent);
        Assert.Equal("0%", Formatting.ShortMetric(snapshot));
        Assert.Equal(3, snapshot.MetricList.Count);
        Assert.Equal("5時間", Formatting.WindowLabel(snapshot.MetricList[0].Label));
        Assert.Equal("初回使用で開始", snapshot.MetricList[0].Hint);
        Assert.Contains("リセット", snapshot.MetricList[2].Hint);
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("リクエスト"));
        Assert.DoesNotContain(snapshot.Lines, line => line.Contains("直近"));
    }

    [Fact]
    public void GoLimitsUrlIsDerivedFromTheSummaryUrl()
    {
        Assert.Equal(
            "https://opencode.ai/console/api/go/status",
            OpenCodeProvider.GoLimitsUrl(new ProviderOptions(null)));
        Assert.Null(OpenCodeProvider.GoLimitsUrl(new ProviderOptions("https://example.test/usage")));
    }

    [Fact]
    public void WorkspacesUrlIsDerivedFromTheSummaryUrl()
    {
        Assert.Equal(
            "https://opencode.ai/console/api/user/workspaces",
            OpenCodeProvider.WorkspacesUrl(new ProviderOptions(null)));
        Assert.Null(OpenCodeProvider.WorkspacesUrl(new ProviderOptions("https://example.test/usage")));
    }

    [Fact]
    public void WorkspaceIdAcceptsTheConsoleUrl()
    {
        Assert.Equal(
            "wrk_01TESTWORKSPACE0000000000",
            OpenCodeProvider.NormalizeWorkspaceId("https://opencode.ai/console/wrk_01TESTWORKSPACE0000000000/go"));
        Assert.Equal(
            "wrk_01TESTWORKSPACE0000000000",
            OpenCodeProvider.NormalizeWorkspaceId("  wrk_01TESTWORKSPACE0000000000  "));
        Assert.Equal(
            "org_abc123",
            OpenCodeProvider.NormalizeWorkspaceId("org_abc123"));
        Assert.Null(OpenCodeProvider.NormalizeWorkspaceId("https://opencode.ai/console"));
        Assert.Null(OpenCodeProvider.NormalizeWorkspaceId(""));
    }

    [Fact]
    public void CollectWorkspaceIdsReadsTheWorkspaceList()
    {
        const string json = """
            [
              { "id": "wrk_01TESTWORKSPACE0000000000", "name": "Personal" },
              { "id": "wrk_BLOCKED", "name": "Blocked", "blocked": true },
              { "id": "not-a-workspace" }
            ]
            """;
        using var doc = JsonDocument.Parse(json);
        var workspaces = OpenCodeProvider.CollectWorkspaceIds(doc.RootElement);
        Assert.Equal(["wrk_01TESTWORKSPACE0000000000"], workspaces);

        using var wrapped = JsonDocument.Parse("""{ "workspaces": [ { "id": "org_abc123" } ] }""");
        Assert.Equal(["org_abc123"], OpenCodeProvider.CollectWorkspaceIds(wrapped.RootElement));
    }

    [Fact]
    public void UnknownShapeIsUnparsed()
    {
        using var doc = JsonDocument.Parse("""{ "unexpected": true }""");
        var snapshot = OpenCodeProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.False(snapshot.Ok);
        Assert.Equal(ProviderErrors.Unparsed, snapshot.Error);
    }

    [Fact]
    public void MissingEndpointStatusIsRecognised()
    {
        Assert.True(OpenCodeProvider.IsMissingEndpoint(404));
        Assert.True(OpenCodeProvider.IsMissingEndpoint(405));
        Assert.False(OpenCodeProvider.IsMissingEndpoint(401));
        Assert.False(OpenCodeProvider.IsMissingEndpoint(500));
    }

    [Fact]
    public void EndpointMissingHasItsOwnShortLabel()
    {
        using var _ = new UiCulture("ja-JP");
        Assert.Equal("URL 未設定", UiText.ErrorLabel(ProviderErrors.EndpointMissing));
        Assert.Equal("OpenCode Go URL 未設定", UiText.CompactError("OpenCode Go", ProviderErrors.EndpointMissing));
    }

    [Fact]
    public void ConfigDoesNotSerializeComputedProviderList()
    {
        var json = JsonSerializer.Serialize(new AppConfig().Normalized());
        Assert.DoesNotContain("EnabledProviders", json);
    }
}

public class ProviderConfigTests
{
    [Fact]
    public void LegacyFlagsMigrateToProviderList()
    {
        var migrated = new AppConfig { ShowCursor = true, ShowCodex = false }.Normalized();
        Assert.Equal(["cursor"], migrated.ShowProviders);
        Assert.True(migrated.ShowCursor);
        Assert.False(migrated.ShowCodex);
        Assert.DoesNotContain("claude", migrated.EnabledProviders);
    }

    [Fact]
    public void ExplicitProviderListIsKeptAndUnknownIdsAreDropped()
    {
        var config = new AppConfig { ShowProviders = ["claude", "unknown", "codex", "claude"] }.Normalized();
        Assert.Equal(["claude", "codex"], config.ShowProviders);
        Assert.False(config.ShowCursor);
        Assert.True(config.ShowCodex);
    }

    [Fact]
    public void ProviderOrderIncludesEveryRegisteredProvider()
    {
        var order = new AppConfig { ProviderOrder = ["codex"] }.Normalized().ProviderOrder;
        Assert.Equal("codex", order[0]);
        Assert.Equal(ProviderRegistry.Ids.Count, order.Count);
        Assert.Equal(ProviderRegistry.Ids.OrderBy(id => id), order.OrderBy(id => id));
        Assert.Contains("claude", order);
        Assert.Contains("opencode", order);
        Assert.Contains("commandcode", order);
    }

    [Fact]
    public void UsageUrlIsTrimmedAndBlankBecomesNull()
    {
        Assert.Equal("https://example.test/usage", new AppConfig { OpenCodeUsageUrl = " https://example.test/usage " }.Normalized().OpenCodeUsageUrl);
        Assert.Null(new AppConfig { OpenCodeUsageUrl = "   " }.Normalized().OpenCodeUsageUrl);
    }

    [Fact]
    public void WorkspaceIdIsExtractedFromAConsoleUrl()
    {
        var config = new AppConfig
        {
            OpenCodeWorkspaceId = "https://opencode.ai/console/wrk_01TESTWORKSPACE0000000000/go",
        }.Normalized();
        Assert.Equal("wrk_01TESTWORKSPACE0000000000", config.OpenCodeWorkspaceId);
        Assert.Equal(
            "wrk_01TESTWORKSPACE0000000000",
            ProviderOptions.From(config).OpenCodeWorkspaceId);
        Assert.Null(new AppConfig { OpenCodeWorkspaceId = "https://opencode.ai/console" }.Normalized().OpenCodeWorkspaceId);
    }

    [Fact]
    public void RegisteredProvidersHaveUniqueIdsAndTitles()
    {
        Assert.Equal(ProviderRegistry.Ids.Count, ProviderRegistry.Ids.Distinct().Count());
        foreach (var id in ProviderRegistry.Ids)
        {
            Assert.NotNull(ProviderRegistry.Find(id));
            Assert.False(string.IsNullOrWhiteSpace(ProviderRegistry.Title(id)));
        }
    }
}

public class ProviderAuthTests
{
    [Fact]
    public void ClaudeCredentialsAreParsedFromOauthBlock()
    {
        var credentials = ClaudeAuth.Parse("""
            {
              "claudeAiOauth": {
                "accessToken": "access",
                "refreshToken": "refresh",
                "expiresAt": 1999999999999
              }
            }
            """);
        Assert.NotNull(credentials);
        Assert.Equal("access", credentials!.AccessToken);
        Assert.Equal("refresh", credentials.RefreshToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1999999999999), credentials.ExpiresAt);
    }

    [Fact]
    public void ClaudeRefreshIsNeededOnlyWhenExpiring()
    {
        var soon = new ClaudeCredentials("a", "r", DateTimeOffset.UnixEpoch.AddMinutes(1));
        Assert.True(ClaudeAuth.NeedsRefresh(soon, DateTimeOffset.UnixEpoch));
        var later = new ClaudeCredentials("a", "r", DateTimeOffset.UnixEpoch.AddHours(2));
        Assert.False(ClaudeAuth.NeedsRefresh(later, DateTimeOffset.UnixEpoch));
        var noRefresh = new ClaudeCredentials("a", null, DateTimeOffset.UnixEpoch.AddMinutes(1));
        Assert.False(ClaudeAuth.NeedsRefresh(noRefresh, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void ClaudeRefreshRequestUsesPublicClientId()
    {
        using var doc = JsonDocument.Parse(ClaudeAuth.RefreshRequestJson("rt"));
        Assert.Equal("refresh_token", doc.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal(ClaudeAuth.ClientId, doc.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("rt", doc.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public void ClaudeApplyRefreshedKeepsOtherFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-claude-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"claudeAiOauth":{"accessToken":"old","refreshToken":"rt","scopes":["user:inference"]}}""");
            ClaudeAuth.ApplyRefreshed(new ClaudeRefreshResult("new", "new-rt", DateTimeOffset.FromUnixTimeSeconds(2000000000)), path);
            var reloaded = ClaudeAuth.Load(path);
            Assert.NotNull(reloaded);
            Assert.Equal("new", reloaded!.AccessToken);
            Assert.Equal("new-rt", reloaded.RefreshToken);
            Assert.Contains("user:inference", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CommandCodeTokenIsFoundInKnownKeyOrNestedObject()
    {
        Assert.Equal("abcdefghij", CommandCodeAuth.ReadToken("""{ "token": "abcdefghij" }"""));
        Assert.Equal("tok-123456", CommandCodeAuth.ReadToken("""{ "auth": { "access_token": "tok-123456" } }"""));
        Assert.Null(CommandCodeAuth.ReadToken("""{ "other": 1 }"""));
    }

    [Fact]
    public void OpenCodeApiKeyPrefersOpencodeGoEntry()
    {
        const string json = """
            {
              "opencode-go": { "type": "api", "key": "oc_sk_go" },
              "opencode": { "type": "api", "key": "oc_sk_default" }
            }
            """;
        Assert.Equal("oc_sk_go", OpenCodeAuth.ReadApiKey(json));
        Assert.Equal("oc_sk_default", OpenCodeAuth.ReadApiKey("""{ "opencode": { "type": "api", "key": "oc_sk_default" } }"""));
        Assert.Null(OpenCodeAuth.ReadApiKey("""{ }"""));
    }

    [Fact]
    public void DeviceTokenSessionKeepsTheWorkspace()
    {
        using var doc = JsonDocument.Parse("""
            {
              "access_token": "access",
              "refresh_token": "refresh",
              "expires_in": 3600,
              "org_id": "wrk_01TESTWORKSPACE0000000000"
            }
            """);
        var session = OpenCodeAuth.SessionFrom(doc.RootElement);
        Assert.NotNull(session);
        Assert.Equal("access", session!.AccessToken);
        Assert.Equal("refresh", session.RefreshToken);
        Assert.Equal("wrk_01TESTWORKSPACE0000000000", session.WorkspaceId);
    }
}
