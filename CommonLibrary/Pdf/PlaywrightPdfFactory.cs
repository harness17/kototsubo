using Microsoft.Playwright;

namespace Dev.CommonLibrary.Pdf;

/// <summary>
/// <see cref="IPlaywright"/> を Singleton で保持するファクトリ。
/// </summary>
/// <remarks>
/// Playwright の初期化は重いため、プロセス内で 1 つを共有して使い回す。
/// Browser は呼び出し側で per-request 起動する。
/// </remarks>
public interface IPlaywrightFactory
{
    /// <summary>共有 <see cref="IPlaywright"/> インスタンスを取得する（初回のみ生成）。</summary>
    Task<IPlaywright> GetAsync();
}

/// <inheritdoc />
public sealed class PlaywrightFactory : IPlaywrightFactory, IAsyncDisposable
{
    private IPlaywright? _instance;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IPlaywright> GetAsync()
    {
        if (_instance is not null) return _instance;

        await _lock.WaitAsync();
        try
        {
            _instance ??= await Playwright.CreateAsync();
            return _instance;
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _instance?.Dispose();
        _instance = null;
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
