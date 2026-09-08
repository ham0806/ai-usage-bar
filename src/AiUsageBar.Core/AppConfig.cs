using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiUsageBar.Core;

public sealed class AppConfig
{
    [JsonPropertyName("refresh_seconds")]
    public int RefreshSeconds { get; set; } = 90;

    [JsonPropertyName("show_cursor")]
    public bool ShowCursor { get; set; } = true;

    [JsonPropertyName("show_codex")]
    public bool ShowCodex { get; set; } = true;

    [JsonPropertyName("start_with_windows")]
    public bool StartWithWindows { get; set; }

    [JsonPropertyName("offset_x")]
    public int OffsetX { get; set; }

    [JsonPropertyName("offset_y")]
    public int OffsetY { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = UiText.SystemLanguage;

    public AppConfig Normalized()
    {
        return new AppConfig
        {
            RefreshSeconds = Math.Clamp(RefreshSeconds, 30, 3600),
            ShowCursor = ShowCursor,
            ShowCodex = ShowCodex,
            StartWithWindows = StartWithWindows,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            Language = UiText.NormalizeLanguage(Language),
        };
    }

    public static AppConfig Load()
    {
        var path = AppPaths.ConfigPath;
        if (!File.Exists(path))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json);
            return (loaded ?? new AppConfig()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        var normalized = Normalized();
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.ConfigPath, json + Environment.NewLine);
    }
}

public static class AppPaths
{
    public const string AppName = "ai-usage-bar";

    public static string ConfigDir
    {
        get
        {
            var local = Environment.GetEnvironmentVariable("LOCALAPPDATA")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
            var path = Path.Combine(local, AppName);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static string IconsDir
    {
        get
        {
            var path = Path.Combine(ConfigDir, "icons");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LogPath
    {
        get
        {
            var temp = Environment.GetEnvironmentVariable("TEMP")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
            return Path.Combine(temp, "ai-usage-bar.log");
        }
    }
}
