using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.ImdbRenamer.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRenamer.Services;

/// <summary>
/// Wyszukuje IMDb ID i kanoniczny tytuł na podstawie tytułu/roku, korzystając z TMDb,
/// a w razie niepowodzenia z OMDb jako fallback.
/// </summary>
public class MetadataResolver
{
    private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
    private const string OmdbBaseUrl = "https://www.omdbapi.com/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MetadataResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataResolver"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Fabryka klientów HTTP dostarczana przez Jellyfin.</param>
    /// <param name="logger">Logger.</param>
    public MetadataResolver(IHttpClientFactory httpClientFactory, ILogger<MetadataResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Próbuje znaleźć IMDb ID i tytuł dla podanego zapytania.
    /// </summary>
    /// <param name="query">Tytuł do wyszukania.</param>
    /// <param name="year">Opcjonalny rok produkcji, jeśli znany.</param>
    /// <param name="isSeries">Czy szukać serialu (true) czy filmu (false).</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    /// <returns>Wynik wyszukiwania albo null, jeśli nic nie znaleziono.</returns>
    public async Task<LookupResult?> ResolveAsync(
        string query,
        int? year,
        bool isSeries,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            var tmdbResult = await ResolveViaTmdbAsync(query, year, isSeries, config.TmdbApiKey, cancellationToken)
                .ConfigureAwait(false);
            if (tmdbResult is not null)
            {
                return tmdbResult;
            }
        }

        if (!string.IsNullOrWhiteSpace(config.OmdbApiKey))
        {
            var omdbResult = await ResolveViaOmdbAsync(query, year, config.OmdbApiKey, cancellationToken)
                .ConfigureAwait(false);
            if (omdbResult is not null)
            {
                return omdbResult;
            }
        }

        _logger.LogWarning("Nie znaleziono metadanych dla \"{Query}\" ({Year})", query, year);
        return null;
    }

    private async Task<LookupResult?> ResolveViaTmdbAsync(
        string query,
        int? year,
        bool isSeries,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            // Token v4 (Read Access Token) to JWT wysyłany przez nagłówek Authorization: Bearer.
            var isBearerToken = apiKey.Count(c => c == '.') == 2;
            var mediaType = isSeries ? "tv" : "movie";
            var encodedQuery = HttpUtility.UrlEncode(query);
            var yearParam = year.HasValue
                ? (isSeries ? $"&first_air_date_year={year}" : $"&year={year}")
                : string.Empty;

            var apiKeyParam = isBearerToken ? string.Empty : $"api_key={apiKey}&";
            var searchUrl =
                $"{TmdbBaseUrl}/search/{mediaType}?{apiKeyParam}language=pl-PL&query={encodedQuery}{yearParam}";

            using var searchRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(searchUrl));
            if (isBearerToken)
            {
                searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var searchResponse = await client.SendAsync(searchRequest, cancellationToken)
                .ConfigureAwait(false);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDb search zwrócił {StatusCode} dla \"{Query}\"", searchResponse.StatusCode, query);
                return null;
            }

            using var searchDoc = JsonDocument.Parse(
                await searchResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

            if (!searchDoc.RootElement.TryGetProperty("results", out var results) ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            var dateField = isSeries ? "first_air_date" : "release_date";
            var titleField = isSeries ? "name" : "title";

            JsonElement? best = null;
            int? bestYear = null;

            // Jeśli znamy rok, odrzucamy dopasowania, których rok znacząco odbiega
            // (zapobiega myleniu sequeli/spin-offów o tym samym tytule).
            var candidateCount = Math.Min(results.GetArrayLength(), 10);
            for (var i = 0; i < candidateCount; i++)
            {
                var candidate = results[i];
                int? candidateYear = null;
                if (candidate.TryGetProperty(dateField, out var candidateDateEl) &&
                    DateTime.TryParse(candidateDateEl.GetString(), out var candidateDate))
                {
                    candidateYear = candidateDate.Year;
                }

                if (!year.HasValue)
                {
                    best = candidate;
                    bestYear = candidateYear;
                    break;
                }

                if (candidateYear.HasValue && Math.Abs(candidateYear.Value - year.Value) <= 1)
                {
                    best = candidate;
                    bestYear = candidateYear;
                    break;
                }
            }

            if (best is null)
            {
                _logger.LogWarning(
                    "Pominięto dopasowanie TMDb dla \"{Query}\" ({Year}) — żaden wynik nie pasował rokiem",
                    query,
                    year);
                return null;
            }

            var first = best.Value;
            var tmdbId = first.GetProperty("id").GetInt32();
            var title = first.GetProperty(titleField).GetString() ?? query;
            var resolvedYear = bestYear;

            var externalIdsUrl = isBearerToken
                ? $"{TmdbBaseUrl}/{mediaType}/{tmdbId}/external_ids"
                : $"{TmdbBaseUrl}/{mediaType}/{tmdbId}/external_ids?api_key={apiKey}";
            using var externalRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(externalIdsUrl));
            if (isBearerToken)
            {
                externalRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var externalResponse = await client.SendAsync(externalRequest, cancellationToken)
                .ConfigureAwait(false);
            if (!externalResponse.IsSuccessStatusCode)
            {
                return null;
            }

            using var externalDoc = JsonDocument.Parse(
                await externalResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

            if (!externalDoc.RootElement.TryGetProperty("imdb_id", out var imdbIdEl) ||
                imdbIdEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var imdbId = imdbIdEl.GetString();
            if (string.IsNullOrWhiteSpace(imdbId))
            {
                return null;
            }

            return new LookupResult
            {
                Title = title,
                Year = resolvedYear ?? year,
                ImdbId = imdbId
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "Błąd podczas zapytania do TMDb dla \"{Query}\"", query);
            return null;
        }
    }

    private async Task<LookupResult?> ResolveViaOmdbAsync(
        string query,
        int? year,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var encodedQuery = HttpUtility.UrlEncode(query);
            var yearParam = year.HasValue ? $"&y={year}" : string.Empty;
            var url = $"{OmdbBaseUrl}?apikey={apiKey}&t={encodedQuery}{yearParam}";

            using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            var root = doc.RootElement;

            if (root.TryGetProperty("Response", out var responseEl) &&
                string.Equals(responseEl.GetString(), "False", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var imdbId = root.TryGetProperty("imdbID", out var imdbIdEl) ? imdbIdEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(imdbId))
            {
                return null;
            }

            var title = root.TryGetProperty("Title", out var titleEl) ? titleEl.GetString() ?? query : query;

            int? resolvedYear = null;
            if (root.TryGetProperty("Year", out var yearEl) &&
                int.TryParse(yearEl.GetString()?.Substring(0, 4), out var parsedYear))
            {
                resolvedYear = parsedYear;
            }

            return new LookupResult
            {
                Title = title,
                Year = resolvedYear ?? year,
                ImdbId = imdbId
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "Błąd podczas zapytania do OMDb dla \"{Query}\"", query);
            return null;
        }
    }
}
