using System.Text.Json;
using System.Text.Json.Serialization;
using AiUsageBar.Core.Providers;

namespace AiUsageBar.Core;

public sealed class AppConfig
{
    [JsonPropertyName("refresh_seconds")]
    public int RefreshSeconds { get; set; } = 90;

    /// <summary>旧形式。読み込み時の移行にだけ使う。</summary>
    [JsonPropertyName("show_cursor")]
    public bool ShowCursor { get; set; } = true;

    /// <summary>旧形式。読み込み時の移行にだけ使う。</summary>
    [JsonPropertyName("show_codex")]
    public bool ShowCodex { get; set; } = true;

    /// <summary>表示するプロバイダ。null のときは旧形式のフラグから作る。</summary>
    [JsonPropertyName("show_providers")]
    public List<string>? ShowProviders { get; set; }

    [JsonPropertyName("provider_order")]
    public List<string> ProviderOrder { get; set; } = ["cursor", "codex"];

    [JsonPropertyName("opencode_usage_url")]
    public string? OpenCodeUsageUrl { get; set; }

    /// <summary>
    /// opencode console のワークスペース。go/status は x-org-id でワークスペースを指定する。
    /// 空ならセッションの org_id か /user/workspaces から自動で決める。
    /// </summary>
    [JsonPropertyName("opencode_workspace_id")]
    public string? OpenCodeWorkspaceId { get; set; }

    [JsonPropertyName("start_with_windows")]
    public bool StartWithWindows { get; set; }

    [JsonPropertyName("offset_x")]
    public int OffsetX { get; set; }

    [JsonPropertyName("offset_y")]
    public int OffsetY { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = UiText.SystemLanguage;

    [JsonIgnore]
    public IReadOnlyList<string> EnabledProviders => ShowProviders ?? [];

    public AppConfig Normalized()
    {
        var providers = NormalizeEnabled(ShowProviders, ShowCursor, ShowCodex);
        return new AppConfig
        {
            RefreshSeconds = Math.Clamp(RefreshSeconds, 30, 3600),
            ShowCursor = providers.Contains("cursor", StringComparer.Ordinal),
            ShowCodex = providers.Contains("codex", StringComparer.Ordinal),
            ShowProviders = providers,
            ProviderOrder = NormalizeProviderOrder(ProviderOrder),
            OpenCodeUsageUrl = string.IsNullOrWhiteSpace(OpenCodeUsageUrl) ? null : OpenCodeUsageUrl.Trim(),
            OpenCodeWorkspaceId = OpenCodeProvider.NormalizeWorkspaceId(OpenCodeWorkspaceId),
            StartWithWindows = StartWithWindows,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            Language = UiText.NormalizeLanguage(Language),
        };
    }

    private static List<string> NormalizeEnabled(List<string>? showProviders, bool showCursor, bool showCodex)
    {
        if (showProviders is null)
        {
            // 旧形式からの移行。新しく増えたプロバイダは既定でオフ。
            var migrated = new List<string>();
            if (showCursor)
            {
                migrated.Add("cursor");
            }

            if (showCodex)
            {
                migrated.Add("codex");
            }

            return migrated;
        }

        return showProviders
            .Select(provider => provider?.Trim() ?? "")
            .Where(ProviderRegistry.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> NormalizeProviderOrder(IEnumerable<string>? order)
    {
        var normalized = (order ?? [])
            .Where(ProviderRegistry.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var provider in ProviderRegistry.Ids)
        {
            if (!normalized.Contains(provider, StringComparer.Ordinal))
            {
                normalized.Add(provider);
            }
        }

        return normalized;
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
