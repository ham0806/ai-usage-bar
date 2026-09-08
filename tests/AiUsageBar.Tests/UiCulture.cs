using System.Globalization;

namespace AiUsageBar.Tests;

internal sealed class UiCulture : IDisposable
{
    private readonly CultureInfo _ui;

    public UiCulture(string name)
    {
        _ui = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _ui;
    }
}
