using InvoiceBuilder.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InvoiceBuilder.Infrastructure.Rendering;

public interface IBrowserPool
{
    Task<IBrowserLease> RentAsync(CancellationToken cancellationToken = default);
}

public interface IBrowserLease : IAsyncDisposable
{
    IBrowser Browser { get; }
}

/// <summary>
/// Chromium instances launched lazily and shared. At most <see cref="PdfOptions.PoolSize"/> renders run at once, and the
/// browser restarts after <see cref="RestartAfterRenders"/> renders or when it has disconnected.
/// </summary>
public sealed partial class PlaywrightBrowserPool(IOptions<PdfOptions> options, ILogger<PlaywrightBrowserPool> logger)
    : IBrowserPool, IAsyncDisposable
{
    public const int RestartAfterRenders = 200;

    private readonly SemaphoreSlim _slots = new(Math.Max(options.Value.PoolSize, 1));
    private readonly SemaphoreSlim _launch = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _renders;
    private int _active;
    private bool _disposed;

    public async Task<IBrowserLease> RentAsync(CancellationToken cancellationToken = default)
    {
        await _slots.WaitAsync(cancellationToken);
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);
            return new Lease(this, browser);
        }
        catch
        {
            _slots.Release();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseBrowserAsync();
        _playwright?.Dispose();
        _slots.Dispose();
        _launch.Dispose();
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        await _launch.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var stale = _browser is { IsConnected: false } || (_renders >= RestartAfterRenders && _active == 0);
            if (stale)
            {
                LogRestart(_renders);
                await CloseBrowserAsync();
            }

            if (_browser is null)
            {
                _playwright ??= await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                _renders = 0;
            }

            _renders++;
            _active++;
            return _browser;
        }
        finally
        {
            _launch.Release();
        }
    }

    private async Task CloseBrowserAsync()
    {
        if (_browser is null)
        {
            return;
        }

        try
        {
            await _browser.CloseAsync();
        }
        catch (PlaywrightException)
        {
            // Already gone; nothing to close.
        }

        _browser = null;
    }

    private void Return()
    {
        Interlocked.Decrement(ref _active);
        _slots.Release();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restarting Chromium after {Renders} renders")]
    private partial void LogRestart(int renders);

    private sealed class Lease(PlaywrightBrowserPool pool, IBrowser browser) : IBrowserLease
    {
        private int _returned;

        public IBrowser Browser => browser;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _returned, 1) == 0)
            {
                pool.Return();
            }

            return ValueTask.CompletedTask;
        }
    }
}
