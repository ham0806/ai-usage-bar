namespace AiUsageBar.Core;

public static class ProviderIconFiles
{
    public static string FileName(string providerId, bool light) =>
        $"{(string.IsNullOrWhiteSpace(providerId) ? "provider" : providerId.Trim().ToLowerInvariant())}-{(light ? "light" : "dark")}.png";

    public static string? Resolve(string providerId, bool light, string? directory = null)
    {
        var dir = directory ?? AppPaths.IconsDir;
        var path = Path.Combine(dir, FileName(providerId, light));
        return File.Exists(path) ? path : null;
    }
}
