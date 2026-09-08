using System.Text.Json;
using AiUsageBar.Core;
using AiUsageBar.Core.Providers;

namespace AiUsageBar.Tests;

public class CodexPayloadTests
{
    [Fact]
    public void CollectsFiveHourAndSevenDayWindows()
    {
        const string json = """
            {
              "rateLimits": {
                "primary": { "usedPercent": 18.2, "windowDurationMins": 300 },
                "secondary": { "usedPercent": 9, "windowDurationMins": 10080 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var windows = CodexProvider.CollectWindows(doc.RootElement);
        Assert.Equal(2, windows.Count);
        Assert.Equal("5h", windows[0].Label);
        Assert.Equal(18.2, windows[0].UsedPercent);
        Assert.Equal("7d", windows[1].Label);
        Assert.Equal(9, windows[1].UsedPercent);
    }

    [Fact]
    public void CompactShowsBothWindows()
    {
        const string json = """
            {
              "rateLimits": {
                "primary": { "usedPercent": 18.2, "windowDurationMins": 300 },
                "secondary": { "usedPercent": 9, "windowDurationMins": 10080 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        using var _ = new UiCulture("ja-JP");
        var snapshot = CodexProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Codex 5h 81.8% · 7d 91%", snapshot.Compact);
        Assert.Equal("18.2%", Formatting.ShortMetric(snapshot));
        Assert.Contains("5h  残り 81.8%", snapshot.Lines[1]);
        Assert.Equal("5h", snapshot.MetricList[0].Label);
        Assert.Contains("残り 81.8%", snapshot.MetricList[0].Hint);
        Assert.Equal(18.2, snapshot.UsedPercent);
    }

    [Fact]
    public void ReadsWhamPrimaryAndSecondaryWindows()
    {
        const string json = """
            {
              "plan_type": "plus",
              "rate_limit": {
                "primary_window": {
                  "used_percent": 27,
                  "limit_window_seconds": 18000,
                  "reset_at": 1782770922
                },
                "secondary_window": {
                  "used_percent": 4,
                  "limit_window_seconds": 604800,
                  "reset_at": 1783357722
                }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Codex 5h 73% · 7d 96%", snapshot.Compact);
        Assert.Equal("27%", Formatting.ShortMetric(snapshot));
        Assert.Contains("Codex  plus", snapshot.Lines);
        Assert.Equal(27, snapshot.UsedPercent);
    }

    [Fact]
    public void LabelsWeeklyWindowWhenItOccupiesPrimarySlot()
    {
        const string json = """
            {
              "rateLimits": {
                "primary": { "usedPercent": 81, "windowDurationMins": 10080 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        var windows = CodexProvider.CollectWindows(doc.RootElement);
        Assert.Single(windows);
        Assert.Equal("7d", windows[0].Label);
        Assert.Equal("Codex 7d 19%", snapshot.Compact);
    }

    [Fact]
    public void LabelsMonthlyWindowFromApproximateDuration()
    {
        const string json = """
            {
              "rateLimits": {
                "primary": { "usedPercent": 20, "windowDurationMins": 10080 },
                "secondary": { "usedPercent": 10, "windowDurationMins": 43800 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var windows = CodexProvider.CollectWindows(doc.RootElement);
        Assert.Equal(2, windows.Count);
        Assert.Equal("7d", windows[0].Label);
        Assert.Equal("30d", windows[1].Label);
        var snapshot = CodexProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Codex 7d 80% · 30d 90%", snapshot.Compact);
    }

    [Fact]
    public void PrefersCodexLimitIdOverChatgpt()
    {
        const string json = """
            {
              "rateLimitsByLimitId": {
                "chatgpt": {
                  "primary": { "usedPercent": 90, "windowDurationMins": 300 }
                },
                "codex": {
                  "primary": { "usedPercent": 12, "windowDurationMins": 300 }
                }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Codex 5h 88%", snapshot.Compact);
        Assert.Equal(12, snapshot.UsedPercent);
    }
}

public class CursorPayloadTests
{
    [Fact]
    public void CompactShowsAutoAndApiPercents()
    {
        const string json = """
            {
              "membershipType": "pro",
              "individualUsage": {
                "plan": { "autoPercentUsed": 42.0, "apiPercentUsed": 18.5, "totalPercentUsed": 40 }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CursorProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Cursor Auto 42% · API 18.5%", snapshot.Compact);
        Assert.Equal("42%", Formatting.ShortMetric(snapshot));
        Assert.Equal("pro", snapshot.Subtitle);
        Assert.Equal("Auto", snapshot.MetricList[0].Label);
        Assert.Equal(42, snapshot.MetricList[0].UsedPercent);
        Assert.Contains("Auto  42%", snapshot.Lines);
        Assert.Contains("API  18.5%", snapshot.Lines);
        Assert.Equal(42, snapshot.UsedPercent);
    }

    [Fact]
    public void ReadsPercentsFromDisplayMessagesWhenPlanMissing()
    {
        const string json = """
            {
              "autoModelSelectedDisplayMessage": "You've used 12% of your included total usage",
              "namedModelSelectedDisplayMessage": "You've used 7% of your included API usage"
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CursorProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Cursor Auto 12% · API 7%", snapshot.Compact);
    }

    [Fact]
    public void UnlimitedPlanShowsInfinity()
    {
        const string json = """{ "isUnlimited": true, "membershipType": "pro" }""";
        using var doc = JsonDocument.Parse(json);
        using var _ = new UiCulture("ja-JP");
        var snapshot = CursorProvider.FromPayload(doc.RootElement, DateTimeOffset.UnixEpoch);
        Assert.Equal("Cursor ∞", snapshot.Compact);
        Assert.Equal("∞", Formatting.ShortMetric(snapshot));
        Assert.Null(snapshot.UsedPercent);
        Assert.Equal("無制限", snapshot.MetricList[0].Hint);
    }
}

