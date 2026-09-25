using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ImdbRenamer.Configuration;

/// <summary>
/// Konfiguracja wtyczki widoczna w Dashboard -&gt; Plugins -&gt; IMDb Renamer.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets klucz API TMDb (https://www.themoviedb.org/settings/api).
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets klucz API OMDb (https://www.omdbapi.com/apikey.aspx), używany jako fallback.
    /// </summary>
    public string OmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether nadpisywać istniejące już IMDb Id.
    /// </summary>
    public bool OverwriteExistingImdbId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether faktycznie zmieniać nazwy plików na dysku.
    /// Jeśli false, wtyczka tylko uzupełnia IMDb Id i loguje proponowaną nazwę pliku.
    /// </summary>
    public bool RenameFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets szablon nazwy pliku. Dostępne tokeny: {Title}, {Year}.
    /// </summary>
    public string FileNameTemplate { get; set; } = "{Title} ({Year})";

    /// <summary>
    /// Gets or sets a value indicating whether obejmować też seriale/odcinki, a nie tylko filmy.
    /// </summary>
    public bool IncludeSeries { get; set; } = true;
}
