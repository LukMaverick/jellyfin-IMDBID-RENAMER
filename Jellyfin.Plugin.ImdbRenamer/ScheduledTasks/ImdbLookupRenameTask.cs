using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ImdbRenamer.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRenamer.ScheduledTasks;

/// <summary>
/// Zadanie, które przechodzi przez bibliotekę, uzupełnia IMDb Id i zmienia nazwy plików.
/// </summary>
public class ImdbLookupRenameTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly MetadataResolver _metadataResolver;
    private readonly ILogger<ImdbLookupRenameTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbLookupRenameTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Manager biblioteki Jellyfin.</param>
    /// <param name="metadataResolver">Serwis wyszukiwania metadanych.</param>
    /// <param name="logger">Logger.</param>
    public ImdbLookupRenameTask(
        ILibraryManager libraryManager,
        MetadataResolver metadataResolver,
        ILogger<ImdbLookupRenameTask> logger)
    {
        _libraryManager = libraryManager;
        _metadataResolver = metadataResolver;
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

            await ProcessItemAsync(item, config, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Domyślnie brak automatycznego harmonogramu — uruchamiane ręcznie z Dashboard.
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

    private async Task ProcessItemAsync(BaseItem item, Configuration.PluginConfiguration config, CancellationToken cancellationToken)
    {
        try
        {
            var hasImdbId = item.ProviderIds.TryGetValue(MetadataProvider.Imdb.ToString(), out var existingImdbId) &&
                             !string.IsNullOrWhiteSpace(existingImdbId);

            if (hasImdbId && !config.OverwriteExistingImdbId)
            {
                return;
            }

            var isSeries = item is Episode;
            var searchTitle = isSeries && item is Episode episode && episode.Series is not null
                ? episode.Series.Name
                : item.Name;

            var yearHint = item.ProductionYear ?? ExtractYearFromPath(item.Path);

            var lookup = await _metadataResolver
                .ResolveAsync(searchTitle, yearHint, isSeries, cancellationToken)
                .ConfigureAwait(false);

            if (lookup is null)
            {
                return;
            }

            item.SetProviderId(MetadataProvider.Imdb, lookup.ImdbId);
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Ustawiono IMDb Id {ImdbId} dla \"{Name}\"",
                lookup.ImdbId,
                item.Name);

            if (config.RenameFiles && !string.IsNullOrWhiteSpace(item.Path) && File.Exists(item.Path))
            {
                var newFileName = FileRenamerService.BuildFileName(
                    config.FileNameTemplate,
                    lookup.Title,
                    lookup.Year ?? yearHint);

                var newPath = FileRenamerService.RenameFile(item.Path, newFileName);
                if (!string.Equals(newPath, item.Path, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Zmieniono nazwę pliku: {Old} -> {New}", item.Path, newPath);
                    item.Path = newPath;
                    await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Nie udało się zmienić nazwy pliku dla \"{Name}\"", item.Name);
        }
    }

    private static int? ExtractYearFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var match = Regex.Match(path, @"(?<!\d)(19\d{2}|20\d{2})(?!\d)");
        return match.Success && int.TryParse(match.Value, out var year) ? year : null;
    }
}
