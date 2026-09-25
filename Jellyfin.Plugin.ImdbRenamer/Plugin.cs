using System.Collections.Generic;
using Jellyfin.Plugin.ImdbRenamer.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ImdbRenamer;

/// <summary>
/// Główna klasa wtyczki Jellyfin IMDb Renamer.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Ścieżki aplikacji Jellyfin.</param>
    /// <param name="xmlSerializer">Serializer XML do zapisu konfiguracji.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "IMDb Renamer";

    /// <inheritdoc />
    public override string Description =>
        "Uzupełnia IMDb ID w metadanych i zmienia nazwy plików wideo na czyste tytuły.";

    /// <inheritdoc />
    public override System.Guid Id => System.Guid.Parse("2f6a7c1e-4b3d-4a9a-9e6b-6c7a3d9b5e21");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace)
        };
    }
}
