using AiUsageBar.Core.Auth;
using AiUsageBar.Core.Providers;

namespace AiUsageBar.Core;

public sealed record ProviderOptions(string? OpenCodeUsageUrl = null, string? OpenCodeWorkspaceId = null)
{
    public static ProviderOptions From(AppConfig config) => new(config.OpenCodeUsageUrl, config.OpenCodeWorkspaceId);
}

/// <summary>
/// 設定画面に出す秘密情報の入力欄。Label は UiText.ProviderFieldLabel で言語切替する。
/// </summary>
public sealed record ProviderCredential(string User);

/// <summary>
/// 表示できるプロバイダの一覧。追加するときはここへ 1 行足す。
/// </summary>
public sealed record ProviderDefinition(
    string Id,
    string Title,
    Func<HttpClient, ProviderOptions, CancellationToken, Task<ProviderSnapshot>> Fetch,
    IReadOnlyList<ProviderCredential>? Credentials = null,
    Func<IReadOnlyList<string>?>? FindCli = null,
    string? LoginArgument = null,
    bool HasUsageUrl = false)
{
    public IReadOnlyList<ProviderCredential> CredentialList => Credentials ?? [];
}

public static class ProviderRegistry
{
    public static IReadOnlyList<ProviderDefinition> All { get; } =
    [
        new(
            "cursor",
            "Cursor",
            (http, _, cancellationToken) => CursorProvider.FetchAsync(http, cancellationToken),
            [new ProviderCredential(CredentialUsers.Cursor)]),
        new(
            "codex",
            "Codex",
            (http, _, cancellationToken) => CodexProvider.FetchAsync(http, cancellationToken),
            FindCli: CodexAuth.FindCli,
            LoginArgument: "login"),
        new(
            "claude",
            "Claude",
            (http, _, cancellationToken) => ClaudeProvider.FetchAsync(http, cancellationToken),
            [new ProviderCredential(CredentialUsers.Claude)]),
        new(
            "opencode",
            "OpenCode Go",
            OpenCodeProvider.FetchAsync,
            [
                new ProviderCredential(CredentialUsers.OpenCode),
                new ProviderCredential(CredentialUsers.OpenCodeCookie),
            ],
            HasUsageUrl: true),
        new(
            "commandcode",
            "Command Code",
            (http, _, cancellationToken) => CommandCodeProvider.FetchAsync(http, cancellationToken),
            [new ProviderCredential(CredentialUsers.CommandCode)]),
    ];

    public static IReadOnlyList<string> Ids { get; } = All.Select(provider => provider.Id).ToList();

    public static ProviderDefinition? Find(string id) =>
        All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.Ordinal));

    public static bool IsKnown(string id) => Find(id) is not null;

    public static string Title(string id) => Find(id)?.Title ?? id;
}
