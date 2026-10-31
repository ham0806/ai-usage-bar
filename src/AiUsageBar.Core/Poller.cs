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
    private bool _disposed;
    private readonly Dictionary<string, double> _nextOk = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderSnapshot> _last = new(StringComparer.Ordinal);

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
        _nextOk.Clear();
        _wake.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        var options = ProviderOptions.From(config);
        var enabled = config.EnabledProviders;
        var now = Environment.TickCount64 / 1000.0;
        var snapshots = new List<ProviderSnapshot>();
        foreach (var provider in config.ProviderOrder)
        {
            if (!enabled.Contains(provider, StringComparer.Ordinal))
            {
                continue;
            }

            var definition = ProviderRegistry.Find(provider);
            if (definition is null)
            {
                continue;
            }

            snapshots.Add(await FetchOneAsync(definition, options, now, cancellationToken).ConfigureAwait(false));
        }

        return snapshots;
    }

    private async Task<ProviderSnapshot> FetchOneAsync(
        ProviderDefinition definition,
        ProviderOptions options,
        double now,
        CancellationToken cancellationToken)
    {
        if (_nextOk.TryGetValue(definition.Id, out var next) && now < next && _last.TryGetValue(definition.Id, out var cached))
        {
            return cached;
        }

        var snapshot = await definition.Fetch(_http, options, cancellationToken).ConfigureAwait(false);
        _last[definition.Id] = snapshot;
        var delay = snapshot.Error switch
        {
            ProviderErrors.RateLimited => 300.0,
            ProviderErrors.AuthRequired => 180.0,
            _ => 0.0,
        };

        _nextOk[definition.Id] = Environment.TickCount64 / 1000.0 + delay;
        return snapshot;
    }
}
