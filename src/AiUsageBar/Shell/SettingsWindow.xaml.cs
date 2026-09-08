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

    public SettingsWindow(AppConfig config, Action<AppConfig> onSave)
    {
        InitializeComponent();
        _onSave = onSave;
        RefreshBox.Text = config.RefreshSeconds.ToString();
        ShowCursorBox.IsChecked = config.ShowCursor;
        ShowCodexBox.IsChecked = config.ShowCodex;
        StartupBox.IsChecked = config.StartWithWindows;
        OffsetXBox.Text = config.OffsetX.ToString();
        OffsetYBox.Text = config.OffsetY.ToString();
        TokenBox.Password = CredentialStore.GetCursorToken() ?? "";
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
    }

    private void ApplyTheme()
    {
        var theme = Themes.Current();
        var fg = Brush(theme.Fg);
        var muted = Brush(theme.Muted);
        var bg = Brush(theme.FlyoutBg);
        var input = Brush(Themes.MixHex(theme.FlyoutBg, theme.Fg, theme.Light ? 0.08 : 0.14));
        var border = Brush(theme.Border);
        var button = Brush(Themes.MixHex(theme.FlyoutBg, theme.Fg, 0.12));

        Background = bg;
        Foreground = fg;
        TitleText.Foreground = fg;
        SubtitleText.Foreground = muted;
        GeneralHeader.Foreground = fg;
        PositionHeader.Foreground = fg;
        AuthHeader.Foreground = fg;
        CodexHeader.Foreground = fg;
        RefreshLabel.Foreground = fg;
        OffsetXLabel.Foreground = fg;
        OffsetYLabel.Foreground = fg;
        TokenLabel.Foreground = fg;
        TokenHint.Foreground = muted;
        CodexHint.Foreground = muted;

        foreach (var box in new[] { GeneralBox, PositionBox, AuthBox, CodexBox })
        {
            box.Foreground = fg;
            box.BorderBrush = border;
            box.Background = Brushes.Transparent;
        }

        foreach (var check in new[] { ShowCursorBox, ShowCodexBox, StartupBox })
        {
            check.Foreground = fg;
        }

        foreach (var field in new[] { RefreshBox, OffsetXBox, OffsetYBox })
        {
            StyleInput(field, fg, input, border);
        }

        TokenBox.Foreground = fg;
        TokenBox.Background = input;
        TokenBox.BorderBrush = border;
        TokenBox.CaretBrush = fg;

        foreach (var buttonControl in new[] { ClearButton, CancelButton, SaveButton, CodexLoginButton })
        {
            buttonControl.Foreground = fg;
            buttonControl.Background = button;
            buttonControl.BorderBrush = border;
        }
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
            var theme = Themes.Current();
            WindowChrome.ApplyWin11Surface(hwnd, dark: !theme.Light, rounded: true, mica: true, border: theme.Border);
        }
        catch (Exception)
        {
        }
    }

    private void ClearToken(object sender, RoutedEventArgs e)
    {
        TokenBox.Password = "";
        CredentialStore.SetCursorToken(null);
    }

    private void LoginCodex(object sender, RoutedEventArgs e)
    {
        var command = CodexAuth.FindCli();
        if (command is null)
        {
            WindowChrome.MessageBox("codex CLI が見つかりません。PATH に codex を入れてから、もう一度実行してください。", "AI Usage Bar");
            return;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command[0],
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            foreach (var arg in command.Skip(1))
            {
                psi.ArgumentList.Add(arg);
            }

            psi.ArgumentList.Add("login");
            System.Diagnostics.Process.Start(psi);
            WindowChrome.MessageBox("ブラウザで Codex にログインしたあと、バーを右クリックして「今すぐ更新」してください。", "AI Usage Bar");
        }
        catch (Exception ex)
        {
            AppLog.Error("codex login を起動できませんでした", ex);
            WindowChrome.MessageBox("codex login を起動できませんでした。", "AI Usage Bar");
        }
    }

    private void Cancel(object sender, RoutedEventArgs e) => Close();

    private void Save(object sender, RoutedEventArgs e)
    {
        var config = new AppConfig
        {
            RefreshSeconds = ParseInt(RefreshBox.Text, 90),
            ShowCursor = ShowCursorBox.IsChecked == true,
            ShowCodex = ShowCodexBox.IsChecked == true,
            StartWithWindows = StartupBox.IsChecked == true,
            OffsetX = ParseInt(OffsetXBox.Text, 0),
            OffsetY = ParseInt(OffsetYBox.Text, 0),
        }.Normalized();
        var token = TokenBox.Password.Trim();
        CredentialStore.SetCursorToken(string.IsNullOrEmpty(token) ? null : token);
        _onSave(config);
        Close();
    }

    private static int ParseInt(string raw, int fallback) => int.TryParse(raw.Trim(), out var value) ? value : fallback;

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
