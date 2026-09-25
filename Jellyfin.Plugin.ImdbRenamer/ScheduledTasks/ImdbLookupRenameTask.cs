using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ImdbRenamer.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRenamer.ScheduledTasks;

/// <summary>
/// Zadanie, które przechodzi przez bibliotekę, uzupełnia IMDb Id i zmienia nazwy plików.
/// </summary>
public class ImdbLookupRenameTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ItemProcessor _itemProcessor;
    private readonly ILogger<ImdbLookupRenameTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbLookupRenameTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Manager biblioteki Jellyfin.</param>
    /// <param name="itemProcessor">Wspólna logika przetwarzania pozycji.</param>
    /// <param name="logger">Logger.</param>
    public ImdbLookupRenameTask(
        ILibraryManager libraryManager,
        ItemProcessor itemProcessor,
        ILogger<ImdbLookupRenameTask> logger)
    {
        _libraryManager = libraryManager;
        _itemProcessor = itemProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Uzupełnij IMDb ID i zmień nazwy plików";

    /// <inheritdoc />
    public string Key => "ImdbRenamerTask";

    /// <inheritdoc />
    public string Description =>
        "Wyszukuje IMDb ID dla filmów/seriali bez uzupełnionego pola i porządkuje nazwy plików.";

    /// <inheritdoc />
    public string Category => "Biblioteka";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey) && string.IsNullOrWhiteSpace(config.OmdbApiKey))
        {
            _logger.LogWarning("Brak skonfigurowanego klucza API TMDb/OMDb — pomijam zadanie.");
            return;
        }

        var items = GetTargetItems(config.IncludeSeries);
        var total = items.Count;
        var processed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress.Report(processed * 100d / Math.Max(total, 1));

            await _itemProcessor.ProcessItemAsync(item, config, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Domyślnie brak automatycznego harmonogramu — nowe pliki są obsługiwane przez LibraryWatcherService.
        return Array.Empty<TaskTriggerInfo>();
    }

    private List<BaseItem> GetTargetItems(bool includeSeries)
    {
        var includeTypes = includeSeries
            ? new[] { nameof(Movie), nameof(Episode) }
            : new[] { nameof(Movie) };

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = includeTypes.Select(t => Enum.Parse<BaseItemKind>(t)).ToArray(),
            IsVirtualItem = false,
            Recursive = true
        };

        return _libraryManager.GetItemList(query).ToList();
    }
}
