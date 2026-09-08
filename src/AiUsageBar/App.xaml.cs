using System.Windows;
using AiUsageBar.Core;
using AiUsageBar.Core.Win32;
using AiUsageBar.Shell;

namespace AiUsageBar;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private TaskbarWidget? _widget;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settings;
    private Poller? _poller;
    private AppConfig _config = new();
    private IReadOnlyList<ProviderSnapshot> _snapshots = [];

    static App()
    {
        WindowChrome.SetDpiAware();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (e.Args.Any(arg => string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase)))
        {
            PreviewCapture.Run();
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, @"Local\AIUsageBarSingleton", out var created);
        if (!created)
        {
            try
            {
                created = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                created = true;
            }
        }

        if (!created)
        {
            WindowChrome.MessageBox("AI Usage Bar はすでに起動しています。", "AI Usage Bar");
            Shutdown();
            return;
        }

        _ownsMutex = true;

        AppLog.Setup();
        _config = AppConfig.Load();
        if (_config.StartWithWindows)
        {
            try
            {
                StartupShortcut.SetEnabled(true);
            }
            catch (Exception)
            {
                AppLog.Warning("スタートアップ登録の同期に失敗しました");
            }
        }

        _widget = new TaskbarWidget(
            () => (_config.OffsetX, _config.OffsetY),
            ToggleFlyout,
            RefreshNow,
            OpenSettings,
            Quit);
        _flyout = new FlyoutWindow();
        _poller = new Poller(() => _config, snapshots => Dispatcher.Invoke(() => ApplySnapshots(snapshots)));
        _widget.Show();
        _poller.Start();
    }

    private void ApplySnapshots(IReadOnlyList<ProviderSnapshot> snapshots)
    {
        _snapshots = snapshots;
        _widget?.SetSnapshots(snapshots);
        if (_flyout is { IsVisible: true } && _widget is not null)
        {
            _flyout.ShowSnapshots(snapshots, _widget);
        }
    }

    private void ToggleFlyout()
    {
        if (_widget is null || _flyout is null)
        {
            return;
        }

        _flyout.Toggle(_snapshots, _widget);
    }

    private void RefreshNow() => _poller?.RefreshNow();

    private void OpenSettings()
    {
        if (_settings is { IsVisible: true })
        {
            _settings.Activate();
            _settings.Topmost = true;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_settings is not null)
                {
                    _settings.Topmost = false;
                }
            };
            timer.Start();
            return;
        }

        try
        {
            _settings = new SettingsWindow(_config, SaveSettings);
            _settings.Closed += (_, _) => _settings = null;
            _settings.Show();
        }
        catch (Exception ex)
        {
            AppLog.Error("設定ウィンドウを開けませんでした", ex);
            WindowChrome.MessageBox("設定画面を開けませんでした。", "AI Usage Bar");
        }
    }

    private void SaveSettings(AppConfig config)
    {
        _config = config;
        config.Save();
        try
        {
            StartupShortcut.SetEnabled(config.StartWithWindows);
        }
        catch (Exception ex)
        {
            AppLog.Error("スタートアップ登録に失敗しました", ex);
            WindowChrome.MessageBox("スタートアップへの登録または解除に失敗しました。", "AI Usage Bar");
        }

        RefreshNow();
        _widget?.Dock();
    }

    private void Quit()
    {
        _poller?.Dispose();
        _widget?.Dispose();
        _flyout?.Close();
        _settings?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _poller?.Dispose();
        _widget?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
