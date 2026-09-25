using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ImdbRenamer.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
namespace Jellyfin.Plugin.ImdbRenamer.Services;

/// <summary>
/// Wspólna logika uzupełniania IMDb Id i zmiany nazwy pliku dla pojedynczej pozycji biblioteki.
/// Używana zarówno przez zadanie zaplanowane, jak i nasłuch zdarzeń biblioteki (nowe pliki).
/// </summary>
public class ItemProcessor
{
    private readonly MetadataResolver _metadataResolver;
    private readonly ILogger<ItemProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemProcessor"/> class.
    /// </summary>
    /// <param name="metadataResolver">Serwis wyszukiwania metadanych.</param>
    /// <param name="logger">Logger.</param>
    public ItemProcessor(MetadataResolver metadataResolver, ILogger<ItemProcessor> logger)
    {
        _metadataResolver = metadataResolver;
        _logger = logger;
    }

    /// <summary>
    /// Uzupełnia IMDb Id i opcjonalnie zmienia nazwę pliku dla podanej pozycji.
    /// </summary>
    /// <param name="item">Pozycja biblioteki (film lub odcinek).</param>
    /// <param name="config">Konfiguracja wtyczki.</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    public async Task ProcessItemAsync(BaseItem item, PluginConfiguration config, CancellationToken cancellationToken)
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
