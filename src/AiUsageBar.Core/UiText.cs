using System.Globalization;
using AiUsageBar.Core.Auth;

namespace AiUsageBar.Core;

public static class UiText
{
    public const string SystemLanguage = "system";
    public const string EnglishLanguage = "en";
    public const string JapaneseLanguage = "ja";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

    public static bool IsJapanese =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);

    public static void Apply(string? language)
    {
        var culture = Resolve(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return SystemLanguage;
        }

        var value = language.Trim().ToLowerInvariant();
        return value is EnglishLanguage or JapaneseLanguage or "ja-jp" or "en-us"
            ? value is "ja-jp" ? JapaneseLanguage : value is "en-us" ? EnglishLanguage : value
            : SystemLanguage;
    }

    public static CultureInfo Resolve(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            EnglishLanguage => CultureInfo.GetCultureInfo("en"),
            JapaneseLanguage => CultureInfo.GetCultureInfo("ja-JP"),
            _ => SystemUiCulture,
        };
    }

    public static string T(string english, string japanese) => IsJapanese ? japanese : english;

    public static string AuthRequired => T("Sign in", "要認証");
    public static string RateLimitedShort => T("Rate limit", "レート制限");
    public static string FetchFailed => T("Failed", "取得失敗");
    public static string Unparsed => T("Unavailable", "取得失敗");
    public static string Unlimited => T("Unlimited", "無制限");
    public static string Unknown => T("Unknown", "不明");
    public static string Soon => T("soon", "まもなく");
    public static string Plan => T("Plan", "プラン");
    public static string OnDemand => T("On-demand", "オンデマンド");
    public static string BillingCycle => T("Billing cycle", "課金周期");
    public static string LastUpdated => T("Updated", "最終更新");
    public static string Remaining => T("left", "残り");
    public static string Reset => T("resets", "リセット");
    public static string Credits => "Credits";
    public static string CreditsAvailable => T("available", "あり");
    public static string ExtraUsage => T("Extra usage", "追加利用");
    public static string CloudMinutes => T("Cloud minutes", "クラウド時間");
    public static string UsedTotal => T("Total used", "累計使用");
    public static string RangeSeparator => T("~", "～");
    public static string ClickForDetails => T("Click for details", "クリックで詳細");
    public static string NoProviders => T("No providers selected", "表示するプロバイダがありません");

    public static string ProviderHint(string id) => id switch
    {
        "cursor" => TokenHint,
        "codex" => CodexHint,
        "claude" => ClaudeHint,
        "opencode" => OpenCodeHint,
        "commandcode" => CommandCodeHint,
        _ => "",
    };

    public static string ProviderTokenLabel(string id) => id switch
    {
        "cursor" => SessionToken,
        "claude" => ClaudeTokenLabel,
        _ => ApiKeyLabel,
    };

    public static string ProviderFieldLabel(string credentialUser) => credentialUser switch
    {
        CredentialUsers.Cursor => SessionToken,
        CredentialUsers.Claude => ClaudeTokenLabel,
        CredentialUsers.OpenCodeCookie => T("Console cookie (optional)", "コンソール Cookie（任意）"),
        _ => ApiKeyLabel,
    };

    public static string MenuRefresh => T("Refresh now(&R)", "今すぐ更新(&R)");
    public static string MenuSettings => T("Settings(&S)", "設定(&S)");
    public static string MenuExit => T("Exit(&X)", "終了(&X)");

    public static string SettingsTitle => T("Settings", "設定");
    public static string SettingsSubtitle => T("Display and refresh", "表示と更新の動作");
    public static string GeneralHeader => T("General", "一般");
    public static string LanguageLabel => T("Language", "言語");
    public static string LanguageSystem => T("Match Windows", "Windows に合わせる");
    public static string LanguageEnglish => "English";
    public static string LanguageJapanese => "日本語";
    public static string RefreshInterval => T("Refresh interval (seconds)", "更新間隔（秒）");
    public static string DisplayProviders => T("Providers", "表示するプロバイダ");
    public static string MoveUp => T("Move up", "上へ");
    public static string MoveDown => T("Move down", "下へ");
    public static string StartWithWindows => T("Start with Windows", "Windows 起動時に開始");
    public static string PositionHeader => T("Position", "位置");
    public static string OffsetX => T("Offset X", "オフセット X");
    public static string OffsetY => T("Offset Y", "オフセット Y");
    public static string SessionToken => T("Session token (optional)", "セッショントークン（任意）");
    public static string TokenHint => T(
        "Paste the cursor.com cookie WorkosCursorSessionToken. Leave empty if the local Cursor app can supply it.",
        "cursor.com の Cookie「WorkosCursorSessionToken」を貼り付けます。ローカルの Cursor から取れる場合は空で構いません。");
    public static string CodexAuthHeader => T("Codex sign-in", "Codex 認証");
    public static string CodexHint => T(
        "Uses the auth.json from your usual codex login. If it expires, sign in again with the button below.",
        "普段使っている codex login の auth.json を使います。期限切れのときは下のボタンからログインし直してください。");
    public static string OpenCodexLogin => T("Open codex login", "codex login を開く");
    public static string ClearSavedToken => T("Remove saved token", "保存したトークンを削除");
    public static string Cancel => T("Cancel", "キャンセル");
    public static string Save => T("Save", "保存");

    public static string ProviderAuthHeader => T("Sign-in", "認証");
    public static string ClaudeTokenLabel => T("OAuth token (optional)", "OAuth トークン（任意）");
    public static string ApiKeyLabel => T("API key (optional)", "API キー（任意）");
    public static string UsageUrlLabel => T("Usage URL", "使用量 URL");
    public static string UsageUrlHint => T(
        "Leave empty for the built-in URL. Set it when you know the endpoint your tool exposes.",
        "空欄なら組み込みの URL を使います。取得先が分かっている場合だけ入れてください。");
    public static string OpenCodeWorkspaceLabel => T("Workspace (optional)", "ワークスペース（任意）");
    public static string OpenCodeWorkspaceHint => T(
        "Leave empty to detect it from the console sign-in. Paste the URL of your Go page (…/console/wrk_…/go) or the workspace ID when you use several workspaces.",
        "空欄なら console のサインイン情報から自動で判定します。複数のワークスペースを使う場合は Go ページの URL（…/console/wrk_…/go）かワークスペース ID を貼り付けてください。");

    public static string ClaudeHint => T(
        "Uses ~/.claude/.credentials.json from Claude Code. Paste the token only when you sign in elsewhere.",
        "Claude Code の ~/.claude/.credentials.json を使います。別の場所でログインしている場合だけトークンを貼ってください。");
    public static string OpenCodeHint => T(
        "Sign in with the button below to get a console session. The Go limits come from the workspace selected in the console. If sign-in fails, paste the console session cookie (DevTools → Network → Request Headers → Cookie).",
        "下のボタンで console のセッションを取得します。Go の上限は console で選んでいるワークスペースから読みます。うまくいかない場合は console のセッション Cookie を貼り付けてください（DevTools → Network → Request Headers → Cookie）。");
    public static string CommandCodeHint => T(
        "Uses ~/.commandcode/auth.json from commandcode login. Paste an API key when you have not signed in on this PC.",
        "commandcode login の ~/.commandcode/auth.json を使います。この PC でログインしていないときは API キーを貼ってください。");

    public static string ClaudeAuthHint => T(
        "Sign in with Claude Code, or paste the OAuth token in Settings.",
        "Claude Code でログインするか、設定から OAuth トークンを貼り付けてください");
    public static string OpenCodeAuthHint => T(
        "Sign in to the opencode console, then paste the console cookie in Settings.",
        "opencode console にログインし、設定へコンソール Cookie を貼り付けてください");
    public static string CommandCodeAuthHint => T(
        "Run commandcode login, or paste the API key in Settings.",
        "commandcode login を実行するか、設定から API キーを貼り付けてください");
    public static string OpenCodeFetchFailedMessage => T(
        "Could not fetch usage. Check the usage URL and API key in Settings.",
        "取得に失敗しました。設定の使用量 URL と API キーを確認してください");
    public static string OpenCodeEndpointHint => T(
        "The usage URL does not exist. Set the real endpoint in Settings.",
        "使用量 URL が存在しません。設定で実際の取得先を指定してください");
    public static string OpenCodeSignIn => T("Sign in to opencode", "opencode にサインイン");
    public static string OpenCodeSignInStarted(string code) => T(
        $"Approve this sign-in in the browser.\n\nCode: {code}",
        $"ブラウザでこのサインインを承認してください。\n\nコード: {code}");
    public static string OpenCodeSignInDone => T(
        "Signed in to the opencode console.",
        "opencode console にサインインしました。");
    public static string OpenCodeSignInFailed => T(
        "Could not sign in to the opencode console.",
        "opencode console にサインインできませんでした。");

    public static string AlreadyRunning => T(
        "AI Usage Bar is already running.",
        "AI Usage Bar はすでに起動しています。");
    public static string SettingsOpenFailed => T("Could not open Settings.", "設定画面を開けませんでした。");
    public static string StartupFailed => T(
        "Could not add or remove the startup shortcut.",
        "スタートアップへの登録または解除に失敗しました。");
    public static string CodexCliMissing => T(
        "codex CLI was not found. Add it to PATH and try again.",
        "codex CLI が見つかりません。PATH に codex を入れてから、もう一度実行してください。");
    public static string CodexLoginStarted => T(
        "Sign in to Codex in the browser, then right-click the bar and choose Refresh now.",
        "ブラウザで Codex にログインしたあと、バーを右クリックして「今すぐ更新」してください。");
    public static string CodexLoginFailed => T("Could not start codex login.", "codex login を起動できませんでした。");

    public static string CursorAuthHint => T(
        "Sign in at cursor.com, or paste the cookie in Settings.",
        "cursor.com にログインするか、設定から Cookie を貼り付けてください");
    public static string CodexAuthHint => T(
        "Sign in with codex login, or open it from Settings.",
        "codex login を実行するか、設定からログインしてください");
    public static string RateLimitedMessage => T(
        "Rate limited. Will retry shortly.",
        "レート制限中です。しばらく待ってから再取得します");
    public static string FetchFailedMessage => T("Could not fetch usage.", "取得に失敗しました");

    public static string ErrorLabel(string? error) => error switch
    {
        ProviderErrors.AuthRequired => AuthRequired,
        ProviderErrors.RateLimited => RateLimitedShort,
        ProviderErrors.FetchFailed => FetchFailed,
        ProviderErrors.Unparsed => Unparsed,
        ProviderErrors.EndpointMissing => T("No endpoint", "URL 未設定"),
        _ => string.IsNullOrEmpty(error) ? "--" : error,
    };

    public static string CompactError(string title, string? error) =>
        $"{title} {ErrorLabel(error)}";

    public static string WindowLabel(string label) => label switch
    {
        "5h" => T("5h", "5時間"),
        "7d" => T("7d", "7日"),
        "1d" => T("1d", "1日"),
        "30d" => T("30d", "30日"),
        "1y" => T("1y", "1年"),
        "Rolling" => T("Rolling", "ローリング"),
        "Weekly" => T("Weekly", "週次"),
        "Monthly" => T("Monthly", "月次"),
        _ => label,
    };

    public static string StartsOnFirstUse => T("starts on first use", "初回使用で開始");

    public static string ResetInDaysHours(int days, int hours) =>
        T($"in {days}d {hours}h", $"{days}日{hours}時間後");

    public static string ResetInHoursMinutes(int hours, int minutes) =>
        T($"in {hours}h {minutes}m", $"{hours}時間{minutes}分後");

    public static string ResetInMinutes(int minutes) =>
        T($"in {minutes}m", $"{minutes}分後");

    public static string UsedOfLimit(double used, double limit) => $"{used:0}/{limit:0}";

    public static string UsedOfLimitSuffix(double used, double limit) =>
        T($" ({used:0}/{limit:0})", $"（{used:0}/{limit:0}）");

    public static string ParentheticalTime(string text) =>
        T($" ({text})", $"（{text}）");

    public static string Labeled(string label, string value) => $"{label}  {value}";

    public static string WindowDetail(string label, string remaining, string reset, string when) =>
        $"{label}  {Remaining} {remaining}  {Reset} {reset}{when}";

    public static string RemainingHint(string percent) => $"{Remaining} {percent}";

    public static string ResetHint(string reset) => $"{Reset} {reset}";

    public static string CreditsUnlimited => Labeled(Credits, Unlimited);

    public static string CreditsPresent => Labeled(Credits, CreditsAvailable);

    public static string Requests => T("Requests", "リクエスト");
    public static string Cost => T("Cost", "コスト");
    public static string Tokens => T("Tokens", "トークン");
    public static string Cache => T("Cache", "キャッシュ");

    public static string LastDays(int days) => T($"Last {days} days", $"直近{days}日");

    /// <summary>1,234 のような桁区切り。</summary>
    public static string Count(double value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>12.3M / 456K のような短い表現。</summary>
    public static string CompactCount(double value) => value switch
    {
        >= 1_000_000_000 => $"{value / 1_000_000_000:0.#}B",
        >= 1_000_000 => $"{value / 1_000_000:0.#}M",
        >= 1_000 => $"{value / 1_000:0.#}K",
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };

    public static string Money(double value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
}
