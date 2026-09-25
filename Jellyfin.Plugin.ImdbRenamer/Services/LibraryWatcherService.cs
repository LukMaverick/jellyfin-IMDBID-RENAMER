using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRenamer.Services;

/// <summary>
/// Nasłuchuje zdarzeń dodania nowych pozycji do biblioteki Jellyfin i automatycznie
/// uzupełnia dla nich IMDb Id oraz (opcjonalnie) zmienia nazwę pliku, bez czekania na harmonogram.
/// </summary>
public class LibraryWatcherService : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ItemProcessor _itemProcessor;
    private readonly ILogger<LibraryWatcherService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryWatcherService"/> class.
    /// </summary>
    /// <param name="libraryManager">Manager biblioteki Jellyfin.</param>
    /// <param name="itemProcessor">Wspólna logika przetwarzania pozycji.</param>
    /// <param name="logger">Logger.</param>
    public LibraryWatcherService(
        ILibraryManager libraryManager,
        ItemProcessor itemProcessor,
        ILogger<LibraryWatcherService> logger)
    {
        _libraryManager = libraryManager;
        _itemProcessor = itemProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        return Task.CompletedTask;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        if (item.IsVirtualItem || (item is not Movie && item is not Episode))
        {
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || (string.IsNullOrWhiteSpace(config.TmdbApiKey) && string.IsNullOrWhiteSpace(config.OmdbApiKey)))
        {
            return;
        }

        if (item is Episode && !config.IncludeSeries)
        {
            return;
        }

        // Odpalane w tle, żeby nie blokować skanowania biblioteki. Opóźnienie daje
        // Jellyfinowi czas na dokończenie zapisu pliku i własnego rozpoznania metadanych.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                await _itemProcessor.ProcessItemAsync(item, config, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas automatycznego przetwarzania nowej pozycji \"{Name}\"", item.Name);
            }
        });
    }
}
