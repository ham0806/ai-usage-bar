using System.Globalization;

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
    public static string RangeSeparator => T("~", "～");
    public static string ClickForDetails => T("Click for details", "クリックで詳細");
    public static string NoProviders => T("No providers selected", "表示するプロバイダがありません");

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
    public static string ShowCursor => T("Show Cursor", "Cursor を表示");
    public static string ShowCodex => T("Show Codex", "Codex を表示");
    public static string StartWithWindows => T("Start with Windows", "Windows 起動時に開始");
    public static string PositionHeader => T("Position", "位置");
    public static string OffsetX => T("Offset X", "オフセット X");
    public static string OffsetY => T("Offset Y", "オフセット Y");
    public static string CursorAuthHeader => T("Cursor sign-in", "Cursor 認証");
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
        _ => label,
    };

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
}
