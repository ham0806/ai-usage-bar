namespace AiUsageBar.Core;

public static class ProviderIconFiles
{
    public static string FileName(string providerId, bool light)
    {
        var prefix = providerId == "codex" ? "codex" : "cursor";
        return light ? $"{prefix}-light.png" : $"{prefix}-dark.png";
    }

    public static string? Resolve(string providerId, bool light, string? directory = null)
    {
        var dir = directory ?? AppPaths.IconsDir;
        var path = Path.Combine(dir, FileName(providerId, light));
        return File.Exists(path) ? path : null;
    }
}
