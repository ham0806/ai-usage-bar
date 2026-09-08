using System.Text.Json;
using AiUsageBar.Core;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Tests;

public class ConfigAndAuthTests
{
    [Fact]
    public void ProviderIconFileNameUsesLightAndDarkVariants()
    {
        Assert.Equal("cursor-dark.png", ProviderIconFiles.FileName("cursor", false));
        Assert.Equal("cursor-light.png", ProviderIconFiles.FileName("cursor", true));
        Assert.Equal("codex-dark.png", ProviderIconFiles.FileName("codex", false));
        Assert.Equal("codex-light.png", ProviderIconFiles.FileName("codex", true));
    }

    [Fact]
    public void ProviderIconResolveReturnsExistingFileOnly()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-icons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "cursor-dark.png");
            File.WriteAllBytes(path, [1, 2, 3]);
            Assert.Equal(path, ProviderIconFiles.Resolve("cursor", false, dir));
            Assert.Null(ProviderIconFiles.Resolve("cursor", true, dir));
            Assert.Null(ProviderIconFiles.Resolve("codex", false, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RefreshSecondsIsClamped()
    {
        Assert.Equal(30, new AppConfig { RefreshSeconds = 1 }.Normalized().RefreshSeconds);
        Assert.Equal(3600, new AppConfig { RefreshSeconds = 99999 }.Normalized().RefreshSeconds);
        Assert.Equal(90, new AppConfig { RefreshSeconds = 90 }.Normalized().RefreshSeconds);
    }

    [Fact]
    public void ShortMetricUsesPrimaryPercent()
    {
        var snapshot = new ProviderSnapshot("cursor", "Cursor", true, "Cursor Auto 42% · API 18.5%", ["Cursor"], null, null, 42);
        Assert.Equal("42%", Formatting.ShortMetric(snapshot));
        Assert.Equal("∞", Formatting.ShortMetric(new ProviderSnapshot("cursor", "Cursor", true, "Cursor ∞", ["Cursor"], null, null, null)));
        using (new UiCulture("ja-JP"))
        {
            Assert.Equal("要認証", Formatting.ShortMetric(new ProviderSnapshot("cursor", "Cursor", false, "Cursor 要認証", ["Cursor"], ProviderErrors.AuthRequired)));
            Assert.Equal("5時間", Formatting.WindowLabel("5h"));
            Assert.Equal("30日", Formatting.WindowLabel("30d"));
        }
    }

    [Fact]
    public void PercentFormattingMatchesPython()
    {
        Assert.Equal("42%", Formatting.Percent(42));
        Assert.Equal("18.5%", Formatting.Percent(18.5));
        Assert.Equal("0%", Formatting.Percent(-3));
    }

    [Fact]
    public void NormalizeSessionCookieKeepsEncodedSeparator()
    {
        var cookie = CursorAuth.NormalizeSessionCookie("user::eyJhbGciOiJIUzI1NiJ9.e30.sig");
        Assert.Equal("user%3A%3AeyJhbGciOiJIUzI1NiJ9.e30.sig", cookie);
    }

    [Fact]
    public void NormalizeSessionCookieStripsOauthProviderPrefixFromSub()
    {
        var jwt = JwtWithSub("google-oauth2|user_01ABC");
        Assert.Equal($"user_01ABC%3A%3A{jwt}", CursorAuth.NormalizeSessionCookie(jwt));
        Assert.Equal("user_01ABC", CursorAuth.WorkosUserId("google-oauth2|user_01ABC"));
        Assert.Equal("user_01ABC", CursorAuth.WorkosUserId("user_01ABC"));
        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.e30.sig", CursorAuth.AccessToken("user_01ABC%3A%3AeyJhbGciOiJIUzI1NiJ9.e30.sig"));
    }

    [Fact]
    public void ReadItemReadsQuotedTokenWithoutCopying()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-test-{Guid.NewGuid():N}.vscdb");
        try
        {
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE ItemTable (key TEXT PRIMARY KEY, value TEXT)";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "INSERT INTO ItemTable (key, value) VALUES ('cursorAuth/accessToken', '\"tok-from-db\"')";
                cmd.ExecuteNonQuery();
            }

            Assert.Equal("tok-from-db", CursorAuth.ReadItem(path, "cursorAuth/accessToken"));
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string JwtWithJson(string json)
    {
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"eyJhbGciOiJIUzI1NiJ9.{payload}.sig";
    }

    private static string JwtWithSub(string sub) => JwtWithJson($"{{\"sub\":\"{sub}\"}}");

    [Fact]
    public void NormalizeSessionCookieAcceptsAlreadyEncoded()
    {
        var raw = "user%3A%3AeyJhbGciOiJIUzI1NiJ9.e30.sig";
        Assert.Equal(raw, CursorAuth.NormalizeSessionCookie(raw));
    }

    [Fact]
    public void LaunchCommandPointsAtCurrentProcess()
    {
        var (target, arguments, workingDirectory) = StartupShortcut.LaunchCommand();
        Assert.Equal(Environment.ProcessPath, target);
        Assert.Equal("", arguments);
        Assert.Equal(Path.GetDirectoryName(Environment.ProcessPath), workingDirectory);
    }

    [Fact]
    public void RefreshRequestJsonMatchesCodexCli()
    {
        using var doc = JsonDocument.Parse(CodexAuth.RefreshRequestJson("rt-test"));
        Assert.Equal("refresh_token", doc.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal(CodexAuth.ClientId, doc.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("rt-test", doc.RootElement.GetProperty("refresh_token").GetString());
        Assert.False(doc.RootElement.TryGetProperty("scope", out _));
    }

    [Fact]
    public void AccessTokenNeedsRefreshWhenExpired()
    {
        var expired = JwtWithJson("""{"exp":1}""");
        Assert.True(CodexAuth.AccessTokenNeedsRefresh(expired, DateTimeOffset.FromUnixTimeSeconds(100)));
        var future = JwtWithJson("""{"exp":4102444800}""");
        Assert.False(CodexAuth.AccessTokenNeedsRefresh(future, DateTimeOffset.FromUnixTimeSeconds(100)));
    }

    [Fact]
    public void AccountIdFromJwtReadsOpenaiAuthClaim()
    {
        var jwt = JwtWithJson("""{"https://api.openai.com/auth":{"chatgpt_account_id":"acc-99"}}""");
        Assert.Equal("acc-99", CodexAuth.AccountIdFromJwt(jwt));
    }

    [Fact]
    public void ApplyRefreshedTokensKeepsOtherFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "auth.json");
        try
        {
            File.WriteAllText(path, """{"auth_mode":"chatgpt","tokens":{"access_token":"old","refresh_token":"rt","id_token":"id"}}""");
            CodexAuth.ApplyRefreshedTokens(new CodexRefreshResult("new-access", "new-rt", "new-id"), path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("chatgpt", doc.RootElement.GetProperty("auth_mode").GetString());
            Assert.Equal("new-access", doc.RootElement.GetProperty("tokens").GetProperty("access_token").GetString());
            Assert.Equal("new-rt", doc.RootElement.GetProperty("tokens").GetProperty("refresh_token").GetString());
            Assert.Equal("new-id", doc.RootElement.GetProperty("tokens").GetProperty("id_token").GetString());
            Assert.False(doc.RootElement.GetProperty("tokens").TryGetProperty("account_id", out _));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void ApplyRefreshedTokensWritesAccountIdFromJwt()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "auth.json");
        try
        {
            File.WriteAllText(path, """{"auth_mode":"chatgpt","tokens":{"access_token":"old","refresh_token":"rt"}}""");
            var access = JwtWithJson("""{"https://api.openai.com/auth":{"chatgpt_account_id":"acc-from-jwt"}}""");
            CodexAuth.ApplyRefreshedTokens(new CodexRefreshResult(access, "rt", null), path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("acc-from-jwt", doc.RootElement.GetProperty("tokens").GetProperty("account_id").GetString());
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
