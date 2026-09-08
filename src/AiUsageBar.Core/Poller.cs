using AiUsageBar.Core.Providers;

namespace AiUsageBar.Core;

public sealed class Poller : IDisposable
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Action<IReadOnlyList<ProviderSnapshot>> _onUpdate;
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private Task? _loop;
    private readonly Dictionary<string, double> _nextOk = new() { ["cursor"] = 0, ["codex"] = 0 };
    private readonly Dictionary<string, ProviderSnapshot> _last = [];

    public Poller(Func<AppConfig> getConfig, Action<IReadOnlyList<ProviderSnapshot>> onUpdate)
    {
        _getConfig = getConfig;
        _onUpdate = onUpdate;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public void Start()
    {
        _loop ??= Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _wake.Set();
    }

    public void RefreshNow()
    {
        _nextOk["cursor"] = 0;
        _nextOk["codex"] = 0;
        _wake.Set();
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
        _cts.Dispose();
        _wake.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshots = await CollectAsync(cancellationToken).ConfigureAwait(false);
                _onUpdate(snapshots);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Error("使用量の更新処理でエラー", ex);
            }

            var config = _getConfig();
            try
            {
                _wake.Reset();
                _wake.Wait(TimeSpan.FromSeconds(Math.Max(30, config.RefreshSeconds)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<List<ProviderSnapshot>> CollectAsync(CancellationToken cancellationToken)
    {
        var config = _getConfig();
        var now = Environment.TickCount64 / 1000.0;
        var snapshots = new List<ProviderSnapshot>();
        if (config.ShowCursor)
        {
            snapshots.Add(await FetchOneAsync("cursor", now, () => CursorProvider.FetchAsync(_http, cancellationToken)).ConfigureAwait(false));
        }

        if (config.ShowCodex)
        {
            snapshots.Add(await FetchOneAsync("codex", now, () => CodexProvider.FetchAsync(_http, cancellationToken)).ConfigureAwait(false));
        }

        return snapshots;
    }

    private async Task<ProviderSnapshot> FetchOneAsync(string key, double now, Func<Task<ProviderSnapshot>> fetcher)
    {
        if (now < _nextOk[key] && _last.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var snapshot = await fetcher().ConfigureAwait(false);
        _last[key] = snapshot;
        var delay = 0.0;
        if (snapshot.Error == ProviderErrors.RateLimited)
        {
            delay = 300;
        }
        else if (snapshot.Error == ProviderErrors.AuthRequired)
        {
            delay = 180;
        }

        _nextOk[key] = Environment.TickCount64 / 1000.0 + delay;
        return snapshot;
    }
}
