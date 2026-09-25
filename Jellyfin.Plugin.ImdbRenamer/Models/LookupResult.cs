namespace Jellyfin.Plugin.ImdbRenamer.Models;

/// <summary>
/// Wynik wyszukania metadanych dla tytułu.
/// </summary>
public class LookupResult
{
    /// <summary>
    /// Gets or sets czysty tytuł zwrócony przez API.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets rok produkcji.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets identyfikator IMDb (np. tt1234567).
    /// </summary>
    public string ImdbId { get; set; } = string.Empty;
}
