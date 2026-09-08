using System.Globalization;
using AiUsageBar.Core;

namespace AiUsageBar.Tests;

public class LocalizationTests
{
    [Fact]
    public void NormalizeLanguageAcceptsAliases()
    {
        Assert.Equal("system", UiText.NormalizeLanguage(null));
        Assert.Equal("system", UiText.NormalizeLanguage(""));
        Assert.Equal("en", UiText.NormalizeLanguage("EN"));
        Assert.Equal("ja", UiText.NormalizeLanguage("ja-JP"));
        Assert.Equal("system", UiText.NormalizeLanguage("fr"));
    }

    [Fact]
    public void ConfigKeepsLanguageWhenNormalized()
    {
        Assert.Equal("ja", new AppConfig { Language = "ja-JP" }.Normalized().Language);
        Assert.Equal("en", new AppConfig { Language = "en" }.Normalized().Language);
        Assert.Equal("system", new AppConfig { Language = "de" }.Normalized().Language);
    }

    [Fact]
    public void JapaneseStringsMatchPreviousCopy()
    {
        using var _ = new UiCulture("ja-JP");
        Assert.Equal("要認証", UiText.ErrorLabel(ProviderErrors.AuthRequired));
        Assert.Equal("レート制限", UiText.ErrorLabel(ProviderErrors.RateLimited));
        Assert.Equal("5時間", Formatting.WindowLabel("5h"));
        Assert.Equal("30日", Formatting.WindowLabel("30d"));
        Assert.Equal("まもなく", Formatting.FormatReset(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Assert.Equal("2日3時間後", Formatting.FormatReset(
            DateTimeOffset.UnixEpoch.AddDays(2).AddHours(3),
            DateTimeOffset.UnixEpoch));
        Assert.Equal("要認証", Formatting.ShortMetric(new ProviderSnapshot(
            "cursor", "Cursor", false, "Cursor 要認証", ["Cursor"], ProviderErrors.AuthRequired)));
    }

    [Fact]
    public void EnglishStringsAreUsedOutsideJapaneseUi()
    {
        using var _ = new UiCulture("en");
        Assert.Equal("Sign in", UiText.ErrorLabel(ProviderErrors.AuthRequired));
        Assert.Equal("Rate limit", UiText.ErrorLabel(ProviderErrors.RateLimited));
        Assert.Equal("Failed", UiText.ErrorLabel(ProviderErrors.FetchFailed));
        Assert.Equal("5h", Formatting.WindowLabel("5h"));
        Assert.Equal("30d", Formatting.WindowLabel("30d"));
        Assert.Equal("soon", Formatting.FormatReset(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Assert.Equal("in 2d 3h", Formatting.FormatReset(
            DateTimeOffset.UnixEpoch.AddDays(2).AddHours(3),
            DateTimeOffset.UnixEpoch));
        Assert.Equal("Sign in", Formatting.ShortMetric(new ProviderSnapshot(
            "cursor", "Cursor", false, "Cursor Sign in", ["Cursor"], ProviderErrors.AuthRequired)));
    }

    [Fact]
    public void ApplySwitchesDefaultThreadUiCulture()
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            UiText.Apply("en");
            Assert.Equal("en", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            UiText.Apply("ja");
            Assert.Equal("ja", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
        }
    }
}
