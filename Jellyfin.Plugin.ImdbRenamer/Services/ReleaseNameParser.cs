using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ImdbRenamer.Services;

/// <summary>
/// Parsuje tytuł i rok bezpośrednio z nazwy folderu/pliku (release name), niezależnie
/// od tego, co Jellyfin sam rozpoznał jako Item.Name. Chroni przed sytuacją, w której
/// błędnie sparsowana przez Jellyfin nazwa (np. przez uszkodzone nawiasy w nazwie
/// folderu) prowadzi do wyszukania zupełnie innego tytułu w TMDb/OMDb.
/// </summary>
public static class ReleaseNameParser
{
    private static readonly Regex YearRegex = new(@"(?<!\d)(19\d{2}|20\d{2})(?!\d)", RegexOptions.Compiled);

    private static readonly Regex ReleaseTagRegex = new(
        @"\b(1080p|720p|2160p|480p|4k|uhd|web-?dl|webrip|bluray|brrip|dvdrip|hdrip|hdtv|" +
        @"x264|x265|h\s?264|h\s?265|hevc|ac3|aac|dts|ddp\d(\.\d)?|atmos|multi|dubbing|dubbed|" +
        @"lektor|napisy|pldub|repack|retail|proper|complete|internal|remux|hdr10?\+?|dv|10bit|8bit|sdr)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BracketGroupRegex = new(@"[\[\(\{][^\]\)\}]*[\]\)\}]", RegexOptions.Compiled);

    /// <summary>
    /// Parsuje tytuł i (opcjonalnie) rok z nazwy folderu lub pliku pozycji.
    /// </summary>
    /// <param name="path">Pełna ścieżka do pliku pozycji.</param>
    /// <returns>Krotka (Title, Year). Title może być pusty, jeśli nie udało się nic wyodrębnić.</returns>
    public static (string Title, int? Year) ParseFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (string.Empty, null);
        }

        // Zawsze bazujemy na nazwie PLIKU (nie folderu) — foldery zbiorcze (kolekcje typu
        // "Kolekcja Epoka lodowcowa 2002-2022") niosłyby dla każdego pliku tę samą, złą nazwę.
        var fileName = Path.GetFileNameWithoutExtension(path);
        var result = Parse(fileName);

        // Nazwa pliku bywa zbyt uboga (np. "movie.mkv") — wtedy sięgamy po folder nadrzędny,
        // o ile nie jest to folder zbiorczy obejmujący wiele pozycji.
        if (result.Title.Length < 2)
        {
            var folderName = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                result = Parse(folderName);
            }
        }

        return result;
    }

    private static (string Title, int? Year) Parse(string raw)
    {
        var yearMatch = YearRegex.Match(raw);
        int? year = yearMatch.Success && int.TryParse(yearMatch.Value, out var y) ? y : null;

        var cutIndex = raw.Length;
        if (yearMatch.Success)
        {
            cutIndex = Math.Min(cutIndex, yearMatch.Index);
        }

        var tagMatch = ReleaseTagRegex.Match(raw);
        if (tagMatch.Success)
        {
            cutIndex = Math.Min(cutIndex, tagMatch.Index);
        }

        var title = raw[..Math.Max(0, cutIndex)];

        // Usuwamy grupy w nawiasach (np. tagi wydania, nazwy grup release'ujących).
        title = BracketGroupRegex.Replace(title, " ");
        // Usuwamy też pojedyncze, niesparowane nawiasy (np. uszkodzone release name "[d-11[Tytuł").
        title = title.Replace('[', ' ').Replace(']', ' ').Replace('(', ' ').Replace(')', ' ');
        title = title.Replace('.', ' ').Replace('_', ' ');
        title = title.Trim(' ', '-', '–', '—', '.');
        title = Regex.Replace(title, @"\s{2,}", " ").Trim();

        return (title, year);
    }
}
