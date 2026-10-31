using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using AiUsageBar.Core;
using AiUsageBar.Core.Auth;
using AiUsageBar.Core.Win32;

namespace AiUsageBar.Shell;

public partial class SettingsWindow : Window
{
    private readonly Action<AppConfig> _onSave;
    private readonly Theme _theme;
    private readonly List<string> _providerOrder;
    private readonly Dictionary<string, ProviderRow> _providerRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PasswordBox> _tokenBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _usageUrlBoxes = new(StringComparer.Ordinal);
    private readonly List<FrameworkElement> _authControls = [];
    private TextBox? _openCodeWorkspaceBox;
    private Button? _openCodeSignInButton;
    private CancellationTokenSource? _openCodeSignInCts;

    public SettingsWindow(AppConfig config, Action<AppConfig> onSave)
        : this(config, onSave, Themes.Current())
    {
    }

    internal SettingsWindow(AppConfig config, Action<AppConfig> onSave, Theme theme)
    {
        InitializeComponent();
        MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 48);
        _onSave = onSave;
        _theme = theme;
        var normalized = config.Normalized();
        _providerOrder = normalized.ProviderOrder;
        RefreshBox.Text = config.RefreshSeconds.ToString();
        BuildProviderRows(normalized);
        BuildAuthPanel(normalized);
        StartupBox.IsChecked = config.StartWithWindows;
        OffsetXBox.Text = config.OffsetX.ToString();
        OffsetYBox.Text = config.OffsetY.ToString();
        _usageUrlBoxes.TryGetValue("opencode", out var usageUrlBox);
        if (usageUrlBox is not null)
        {
            usageUrlBox.Text = normalized.OpenCodeUsageUrl ?? "";
        }

        if (_openCodeWorkspaceBox is not null)
        {
            _openCodeWorkspaceBox.Text = normalized.OpenCodeWorkspaceId ?? "";
        }

        SelectLanguage(config.Language);
        ApplyStrings();
        ApplyTheme();
        SourceInitialized += (_, _) => ApplyChrome();
        Loaded += (_, _) =>
        {
            Topmost = true;
            Activate();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Topmost = false;
            };
            timer.Start();
        };
        Closed += (_, _) => _openCodeSignInCts?.Cancel();
    }

    private sealed record ProviderRow(CheckBox CheckBox, Button Up, Button Down, DockPanel Panel);

    private void ApplyTheme()
    {
        var theme = _theme;
        var fg = Brush(theme.Fg);
        var muted = Brush(theme.Muted);
        var bg = Brush(theme.FlyoutBg);
        var input = Brush(Themes.MixHex(theme.FlyoutBg, theme.Fg, theme.Light ? 0.12 : 0.22));
        var border = Brush(theme.Border);
        var button = Brush(Themes.MixHex(theme.FlyoutBg, theme.Fg, 0.12));

        Background = bg;
        Foreground = fg;
        TitleText.Foreground = fg;
        SubtitleText.Foreground = muted;
        GeneralHeader.Foreground = fg;
        DisplayProvidersLabel.Foreground = fg;
        PositionHeader.Foreground = fg;
        AuthHeader.Foreground = fg;
        RefreshLabel.Foreground = fg;
        OffsetXLabel.Foreground = fg;
        OffsetYLabel.Foreground = fg;
        LanguageLabel.Foreground = fg;

        foreach (var box in new[] { GeneralBox, PositionBox, AuthBox })
        {
            box.Foreground = fg;
            box.BorderBrush = border;
            box.Background = Brushes.Transparent;
        }

        foreach (Control check in new Control[] { StartupBox, LanguageSystemRadio, LanguageEnglishRadio, LanguageJapaneseRadio })
        {
            check.Foreground = fg;
        }

        foreach (var row in _providerRows.Values)
        {
            row.CheckBox.Foreground = fg;
        }

        foreach (var field in new[] { RefreshBox, OffsetXBox, OffsetYBox })
        {
            StyleInput(field, fg, input, border);
        }

        foreach (var tokenBox in _tokenBoxes.Values)
        {
            tokenBox.Foreground = fg;
            tokenBox.Background = input;
            tokenBox.BorderBrush = border;
            tokenBox.CaretBrush = fg;
        }

        foreach (var urlBox in _usageUrlBoxes.Values)
        {
            StyleInput(urlBox, fg, input, border);
        }

        if (_openCodeWorkspaceBox is not null)
        {
            StyleInput(_openCodeWorkspaceBox, fg, input, border);
        }

        foreach (var control in _authControls.Concat(new FrameworkElement[] { ClearButton, CancelButton, SaveButton })
                     .Concat(_providerRows.Values.Select(row => row.Up))
                     .Concat(_providerRows.Values.Select(row => row.Down)))
        {
            switch (control)
            {
                case Button buttonControl:
                    buttonControl.Foreground = fg;
                    buttonControl.Background = button;
                    buttonControl.BorderBrush = border;
                    break;
                case TextBlock textControl:
                    textControl.Foreground = Equals(textControl.Tag, "muted") ? muted : fg;
                    break;
            }
        }
    }

    private void ApplyStrings()
    {
        TitleText.Text = UiText.SettingsTitle;
        SubtitleText.Text = UiText.SettingsSubtitle;
        GeneralHeader.Text = UiText.GeneralHeader;
        LanguageLabel.Text = UiText.LanguageLabel;
        LanguageSystemRadio.Content = UiText.LanguageSystem;
        LanguageEnglishRadio.Content = UiText.LanguageEnglish;
        LanguageJapaneseRadio.Content = UiText.LanguageJapanese;
        RefreshLabel.Text = UiText.RefreshInterval;
        DisplayProvidersLabel.Text = UiText.DisplayProviders;
        StartupBox.Content = UiText.StartWithWindows;
        PositionHeader.Text = UiText.PositionHeader;
        OffsetXLabel.Text = UiText.OffsetX;
        OffsetYLabel.Text = UiText.OffsetY;
        AuthHeader.Text = UiText.ProviderAuthHeader;
        ClearButton.Content = UiText.ClearSavedToken;
        CancelButton.Content = UiText.Cancel;
        SaveButton.Content = UiText.Save;
        foreach (var (id, row) in _providerRows)
        {
            row.CheckBox.Content = ProviderRegistry.Title(id);
            row.Up.Content = UiText.MoveUp;
            row.Down.Content = UiText.MoveDown;
        }
    }

    private void SelectLanguage(string language)
    {
        var selected = UiText.NormalizeLanguage(language);
        LanguageSystemRadio.IsChecked = selected == UiText.SystemLanguage;
        LanguageEnglishRadio.IsChecked = selected == UiText.EnglishLanguage;
        LanguageJapaneseRadio.IsChecked = selected == UiText.JapaneseLanguage;
        if (LanguageSystemRadio.IsChecked != true && LanguageEnglishRadio.IsChecked != true && LanguageJapaneseRadio.IsChecked != true)
        {
            LanguageSystemRadio.IsChecked = true;
        }
    }

    private string SelectedLanguage()
    {
        if (LanguageEnglishRadio.IsChecked == true)
        {
            return UiText.EnglishLanguage;
        }

        if (LanguageJapaneseRadio.IsChecked == true)
        {
            return UiText.JapaneseLanguage;
        }

        return UiText.SystemLanguage;
    }

    private static void StyleInput(TextBox field, Brush fg, Brush input, Brush border)
    {
        field.Foreground = fg;
        field.Background = input;
        field.BorderBrush = border;
        field.CaretBrush = fg;
    }

    private void ApplyChrome()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var theme = _theme;
            WindowChrome.ApplyWin11Surface(hwnd, dark: !theme.Light, rounded: true, mica: true, border: theme.Border);
        }
        catch (Exception)
        {
        }
    }

    private void ClearToken(object sender, RoutedEventArgs e)
    {
        foreach (var (user, box) in _tokenBoxes)
        {
            box.Password = "";
            CredentialStore.SetToken(user, null);
        }

        OpenCodeAuth.SaveSession(null);
    }

    /// <summary>
    /// opencode の console は device 認証（CLI と同じ方式）でサインインする。
    /// </summary>
    private async void SignInOpenCode(object sender, RoutedEventArgs e)
    {
        if (_openCodeSignInCts is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _openCodeSignInCts = cts;
        if (_openCodeSignInButton is not null)
        {
            _openCodeSignInButton.IsEnabled = false;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var code = await OpenCodeAuth.RequestDeviceCodeAsync(http, cts.Token);
            if (code is null)
            {
                if (!cts.IsCancellationRequested)
                {
                    WindowChrome.MessageBox(UiText.OpenCodeSignInFailed, "AI Usage Bar");
                }

                return;
            }

            var polling = OpenCodeAuth.PollDeviceTokenAsync(http, code, cts.Token);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = code.VerificationUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLog.Warning($"opencode のサインイン ページを開けませんでした ({ex.GetType().Name})");
            }

            WindowChrome.MessageBox(UiText.OpenCodeSignInStarted(code.UserCode), "AI Usage Bar");
            var session = await polling;
            if (session is null)
            {
                if (!cts.IsCancellationRequested)
                {
                    WindowChrome.MessageBox(UiText.OpenCodeSignInFailed, "AI Usage Bar");
                }

                return;
            }

            OpenCodeAuth.SaveSession(session);
            WindowChrome.MessageBox(UiText.OpenCodeSignInDone, "AI Usage Bar");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _openCodeSignInCts = null;
            cts.Dispose();
            if (_openCodeSignInButton is not null)
            {
                _openCodeSignInButton.IsEnabled = true;
            }
        }
    }

    private void LoginProvider(string providerId)
    {
        var definition = ProviderRegistry.Find(providerId);
        var command = definition?.FindCli?.Invoke();
        if (definition?.LoginArgument is null || command is null)
        {
            WindowChrome.MessageBox(UiText.CodexCliMissing, "AI Usage Bar");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command[0],
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            foreach (var arg in command.Skip(1))
            {
                psi.ArgumentList.Add(arg);
            }

            psi.ArgumentList.Add(definition.LoginArgument);
            Process.Start(psi);
            WindowChrome.MessageBox(UiText.CodexLoginStarted, "AI Usage Bar");
        }
        catch (Exception ex)
        {
            AppLog.Error($"{providerId} のログインを起動できませんでした", ex);
            WindowChrome.MessageBox(UiText.CodexLoginFailed, "AI Usage Bar");
        }
    }

    private void Cancel(object sender, RoutedEventArgs e) => Close();

    private void Save(object sender, RoutedEventArgs e)
    {
        var enabled = _providerOrder
            .Where(id => _providerRows.TryGetValue(id, out var row) && row.CheckBox.IsChecked == true)
            .ToList();
        var config = new AppConfig
        {
            RefreshSeconds = ParseInt(RefreshBox.Text, 90),
            ShowProviders = enabled,
            ProviderOrder = [.. _providerOrder],
            OpenCodeUsageUrl = _usageUrlBoxes.TryGetValue("opencode", out var urlBox) ? urlBox.Text : null,
            OpenCodeWorkspaceId = _openCodeWorkspaceBox?.Text,
            StartWithWindows = StartupBox.IsChecked == true,
            OffsetX = ParseInt(OffsetXBox.Text, 0),
            OffsetY = ParseInt(OffsetYBox.Text, 0),
            Language = SelectedLanguage(),
        }.Normalized();
        foreach (var (user, box) in _tokenBoxes)
        {
            var token = box.Password.Trim();
            CredentialStore.SetToken(user, string.IsNullOrEmpty(token) ? null : token);
        }

        _onSave(config);
        Close();
    }

    private void BuildProviderRows(AppConfig config)
    {
        foreach (var definition in ProviderRegistry.All)
        {
            var checkBox = new CheckBox
            {
                IsChecked = config.EnabledProviders.Contains(definition.Id, StringComparer.Ordinal),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4),
            };
            var up = CreateOrderButton(() => MoveProvider(definition.Id, -1));
            var down = CreateOrderButton(() => MoveProvider(definition.Id, 1));
            var panel = CreateProviderRow(checkBox, up, down);
            _providerRows[definition.Id] = new ProviderRow(checkBox, up, down, panel);
        }

        RefreshProviderOrder();
    }

    private void BuildAuthPanel(AppConfig config)
    {
        AuthPanel.Children.Clear();
        _tokenBoxes.Clear();
        _usageUrlBoxes.Clear();
        _authControls.Clear();
        var first = true;
        foreach (var definition in ProviderRegistry.All)
        {
            if (definition.CredentialList.Count == 0 && definition.FindCli is null)
            {
                continue;
            }

            if (!first)
            {
                AuthPanel.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 10, 0, 10), Background = Brush(_theme.Border) });
            }

            first = false;
            var title = new TextBlock { Text = definition.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2) };
            title.Tag = "fg";
            _authControls.Add(title);
            AuthPanel.Children.Add(title);

            var hint = new TextBlock
            {
                Text = UiText.ProviderHint(definition.Id),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
            };
            hint.Tag = "muted";
            _authControls.Add(hint);
            AuthPanel.Children.Add(hint);

            if (definition.Id == "opencode")
            {
                var signIn = new Button
                {
                    Content = UiText.OpenCodeSignIn,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                signIn.Click += SignInOpenCode;
                _openCodeSignInButton = signIn;
                _authControls.Add(signIn);
                AuthPanel.Children.Add(signIn);
            }

            if (definition.FindCli is not null)
            {
                var loginButton = new Button
                {
                    Content = UiText.OpenCodexLogin,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var id = definition.Id;
                loginButton.Click += (_, _) => LoginProvider(id);
                _authControls.Add(loginButton);
                AuthPanel.Children.Add(loginButton);
            }

            foreach (var credential in definition.CredentialList)
            {
                var label = new TextBlock { Text = UiText.ProviderFieldLabel(credential.User), Margin = new Thickness(0, 8, 0, 2) };
                label.Tag = "fg";
                _authControls.Add(label);
                AuthPanel.Children.Add(label);

                var tokenBox = new PasswordBox { Margin = new Thickness(0, 2, 0, 0) };
                tokenBox.Password = CredentialStore.GetToken(credential.User) ?? "";
                _tokenBoxes[credential.User] = tokenBox;
                _authControls.Add(tokenBox);
                AuthPanel.Children.Add(tokenBox);
            }

            if (definition.Id == "opencode")
            {
                var workspaceLabel = new TextBlock { Text = UiText.OpenCodeWorkspaceLabel, Margin = new Thickness(0, 8, 0, 2) };
                workspaceLabel.Tag = "fg";
                _authControls.Add(workspaceLabel);
                AuthPanel.Children.Add(workspaceLabel);

                _openCodeWorkspaceBox = new TextBox { Margin = new Thickness(0, 2, 0, 0) };
                _authControls.Add(_openCodeWorkspaceBox);
                AuthPanel.Children.Add(_openCodeWorkspaceBox);

                var workspaceHint = new TextBlock { Text = UiText.OpenCodeWorkspaceHint, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
                workspaceHint.Tag = "muted";
                _authControls.Add(workspaceHint);
                AuthPanel.Children.Add(workspaceHint);
            }

            if (definition.HasUsageUrl)
            {
                var label = new TextBlock { Text = UiText.UsageUrlLabel, Margin = new Thickness(0, 8, 0, 2) };
                label.Tag = "fg";
                _authControls.Add(label);
                AuthPanel.Children.Add(label);

                var urlBox = new TextBox { Margin = new Thickness(0, 2, 0, 0) };
                _usageUrlBoxes[definition.Id] = urlBox;
                _authControls.Add(urlBox);
                AuthPanel.Children.Add(urlBox);

                var urlHint = new TextBlock { Text = UiText.UsageUrlHint, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
                urlHint.Tag = "muted";
                _authControls.Add(urlHint);
                AuthPanel.Children.Add(urlHint);
            }
        }
    }

    private static Button CreateOrderButton(Action action)
    {
        var button = new Button { Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(4, 0, 0, 0), MinWidth = 60 };
        button.Click += (_, _) => action();
        return button;
    }

    private static DockPanel CreateProviderRow(CheckBox checkBox, Button up, Button down)
    {
        var row = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(checkBox);
        return row;
    }

    private void MoveProvider(string provider, int offset)
    {
        var index = _providerOrder.IndexOf(provider);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _providerOrder.Count)
        {
            return;
        }

        (_providerOrder[index], _providerOrder[target]) = (_providerOrder[target], _providerOrder[index]);
        RefreshProviderOrder();
    }

    private void RefreshProviderOrder()
    {
        ProviderListPanel.Children.Clear();
        foreach (var provider in _providerOrder)
        {
            if (_providerRows.TryGetValue(provider, out var row))
            {
                ProviderListPanel.Children.Add(row.Panel);
            }
        }

        var last = _providerOrder.Count - 1;
        foreach (var (id, row) in _providerRows)
        {
            row.Up.IsEnabled = _providerOrder[0] != id;
            row.Down.IsEnabled = _providerOrder[last] != id;
        }
    }

    private static int ParseInt(string raw, int fallback) => int.TryParse(raw.Trim(), out var value) ? value : fallback;

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
