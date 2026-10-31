namespace AiUsageBar.Core.Auth;

/// <summary>
/// Credential Manager のユーザー名。プロバイダごとに 1 つ持つ。
/// </summary>
public static class CredentialUsers
{
    public const string Cursor = "cursor-session-token";
    public const string Claude = "claude-oauth-token";
    public const string OpenCode = "opencode-api-key";
    public const string OpenCodeCookie = "opencode-console-cookie";
    public const string OpenCodeConsole = "opencode-console-session";
    public const string CommandCode = "commandcode-api-key";

    public static IReadOnlyList<string> All { get; } = [Cursor, Claude, OpenCode, OpenCodeCookie, OpenCodeConsole, CommandCode];
}
