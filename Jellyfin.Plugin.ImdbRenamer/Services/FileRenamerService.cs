using System;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.ImdbRenamer.Services;

/// <summary>
/// Odpowiada za bezpieczne budowanie nazw plików i faktyczną zmianę nazwy na dysku.
/// </summary>
public static class FileRenamerService
{
    /// <summary>
    /// Buduje bezpieczną nazwę pliku (bez rozszerzenia) na podstawie szablonu.
    /// </summary>
    /// <param name="template">Szablon z tokenami {Title} i {Year}.</param>
    /// <param name="title">Tytuł.</param>
    /// <param name="year">Rok, jeśli znany.</param>
    /// <returns>Nazwa pliku bez rozszerzenia, oczyszczona ze znaków niedozwolonych.</returns>
    public static string BuildFileName(string template, string title, int? year)
    {
        var name = template
            .Replace("{Title}", title, StringComparison.Ordinal)
            .Replace("{Year}", year?.ToString() ?? string.Empty, StringComparison.Ordinal);

        name = name.Replace("()", string.Empty, StringComparison.Ordinal).Trim();

        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
    }

    /// <summary>
    /// Zmienia nazwę pliku na dysku, zachowując folder i rozszerzenie. Nie nadpisuje istniejących plików.
    /// </summary>
    /// <param name="currentPath">Bieżąca pełna ścieżka pliku.</param>
    /// <param name="newFileNameWithoutExtension">Nowa nazwa pliku bez rozszerzenia.</param>
    /// <returns>Nowa pełna ścieżka pliku, albo bieżąca ścieżka, jeśli zmiana nie była potrzebna/możliwa.</returns>
    public static string RenameFile(string currentPath, string newFileNameWithoutExtension)
    {
        var directory = Path.GetDirectoryName(currentPath);
        var extension = Path.GetExtension(currentPath);
        if (directory is null || string.IsNullOrWhiteSpace(newFileNameWithoutExtension))
        {
            return currentPath;
        }

        var candidatePath = Path.Combine(directory, newFileNameWithoutExtension + extension);
        if (string.Equals(candidatePath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return currentPath;
        }

        if (File.Exists(candidatePath))
        {
            // Unikamy nadpisania istniejącego pliku o tej samej docelowej nazwie.
            return currentPath;
        }

        File.Move(currentPath, candidatePath);
        return candidatePath;
    }
}
