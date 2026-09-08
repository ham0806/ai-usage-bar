using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AiUsageBar.Core.Auth;

public static class CursorAuth
{
    private static readonly string[] AccessTokenKeys =
    [
        "cursorAuth/accessToken",
        "cursorAuth/cachedAccessToken",
    ];

    public static string StateDbPath
    {
        get
        {
            var appdata = Environment.GetEnvironmentVariable("APPDATA")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
            return Path.Combine(appdata, "Cursor", "User", "globalStorage", "state.vscdb");
        }
    }

    public static string? NormalizeSessionCookie(string raw)
    {
        var text = raw.Trim().Trim('"');
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("WorkosCursorSessionToken=", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Split('=', 2)[1].Trim();
        }

        text = Uri.UnescapeDataString(text);
        if (text.Contains("::", StringComparison.Ordinal))
        {
            var parts = text.Split("::", 2);
            return $"{WorkosUserId(parts[0])}%3A%3A{parts[1]}";
        }

        if (text.Contains("%3A%3A", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split("%3A%3A", 2, StringSplitOptions.None);
            return parts.Length == 2 ? $"{WorkosUserId(Uri.UnescapeDataString(parts[0]))}%3A%3A{parts[1]}" : text;
        }

        if (LooksLikeJwt(text))
        {
            var payload = Formatting.JwtPayload(text);
            if (payload.TryGetValue("sub", out var sub) && sub.ValueKind == JsonValueKind.String)
            {
                var userId = WorkosUserId(sub.GetString());
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"{userId}%3A%3A{text}";
                }
            }

            return text;
        }

        return text;
    }

    public static string AccessToken(string cookieOrToken)
    {
        const string separator = "%3A%3A";
        var index = cookieOrToken.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? cookieOrToken[(index + separator.Length)..] : cookieOrToken;
    }

    internal static string WorkosUserId(string? sub)
    {
        var text = sub?.Trim() ?? "";
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var index = text.LastIndexOf('|');
        return index >= 0 ? text[(index + 1)..] : text;
    }

    public static string? LoadCursorCookie(bool preferSaved = false)
    {
        var saved = CredentialStore.GetCursorToken();
        string? dbToken = null;
        var dbPath = StateDbPath;
        if (File.Exists(dbPath))
        {
            foreach (var key in AccessTokenKeys)
            {
                var raw = ReadItem(dbPath, key);
                if (!string.IsNullOrEmpty(raw))
                {
                    dbToken = raw;
                    break;
                }
            }

            if (dbToken is null)
            {
                AppLog.Info("Cursor の state.vscdb に accessToken がありませんでした");
            }
        }
        else
        {
            AppLog.Info("Cursor の state.vscdb が見つかりません");
        }

        var candidates = preferSaved ? new[] { saved, dbToken } : new[] { dbToken, saved };
        foreach (var raw in candidates)
        {
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            var cookie = NormalizeSessionCookie(raw);
            if (!string.IsNullOrEmpty(cookie))
            {
                return cookie;
            }
        }

        return null;
    }

    private static bool LooksLikeJwt(string token) => token.StartsWith("eyJ", StringComparison.Ordinal) && token.Count(c => c == '.') >= 2;

    internal static string? ReadItem(string dbPath, string key)
    {
        foreach (var connectionString in ReadOnlyConnectionStrings(dbPath))
        {
            try
            {
                using var conn = new SqliteConnection(connectionString);
                conn.DefaultTimeout = 5;
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
                cmd.Parameters.AddWithValue("$key", key);
                var value = cmd.ExecuteScalar();
                return NormalizeStoredValue(value);
            }
            catch (Exception ex) when (ex is IOException or SqliteException or UnauthorizedAccessException)
            {
                AppLog.Warning($"Cursor の state.vscdb を直接読めませんでした ({ex.GetType().Name})");
            }
        }

        const long copyLimitBytes = 80L * 1024 * 1024;
        try
        {
            if (new FileInfo(dbPath).Length > copyLimitBytes)
            {
                AppLog.Warning("Cursor の state.vscdb が大きいためコピーせず読み取りを諦めます");
                return null;
            }
        }
        catch (IOException ex)
        {
            AppLog.Warning($"Cursor の state.vscdb を確認できませんでした ({ex.GetType().Name})");
            return null;
        }

        var tmpPath = Path.Combine(Path.GetTempPath(), $"ai-usage-bar-{Guid.NewGuid():N}.vscdb");
        try
        {
            File.Copy(dbPath, tmpPath, overwrite: true);
            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = tmpPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            return NormalizeStoredValue(cmd.ExecuteScalar());
        }
        catch (Exception ex) when (ex is IOException or SqliteException or UnauthorizedAccessException)
        {
            AppLog.Warning($"Cursor の state.vscdb のコピー読み取りに失敗しました ({ex.GetType().Name})");
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static IEnumerable<string> ReadOnlyConnectionStrings(string dbPath)
    {
        yield return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        }.ToString();

        yield return new SqliteConnectionStringBuilder
        {
            DataSource = ToSqliteUri(dbPath, "mode=ro&immutable=1"),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
    }

    private static string ToSqliteUri(string path, string query)
    {
        var full = Path.GetFullPath(path);
        var uri = new Uri(full).AbsoluteUri;
        return $"{uri}?{query}";
    }

    private static string? NormalizeStoredValue(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        var text = value is byte[] bytes ? System.Text.Encoding.UTF8.GetString(bytes) : Convert.ToString(value);
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith('"') && text.EndsWith('"') && text.Length >= 2)
        {
            text = text[1..^1];
        }

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
