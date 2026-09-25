# Jellyfin Plugin: IMDb Renamer

Wtyczka do Jellyfin, która automatycznie:

1. Wyszukuje film/serial po tytule i roku w **TMDb** (a jeśli się nie uda — w **OMDb**),
2. Uzupełnia pole **IMDb Id** w metadanych pozycji w bibliotece Jellyfin,
3. Zmienia nazwę pliku wideo na czysty format `Tytuł (Rok).ext` (bez zmiany folderu).

## Wymagania

- Jellyfin Server **10.9.x** (target framework `net8.0`). Jeśli masz inną wersję serwera,
  zmień wersje pakietów `Jellyfin.Controller` / `Jellyfin.Model` w
  [Jellyfin.Plugin.ImdbRenamer.csproj](Jellyfin.Plugin.ImdbRenamer/Jellyfin.Plugin.ImdbRenamer.csproj)
  na wersję zgodną z Twoim serwerem (musi się zgadzać z numerem wersji Jellyfin).
- .NET 8 SDK do budowania.
- Darmowy klucz API z [TMDb](https://www.themoviedb.org/settings/api) (i opcjonalnie z [OMDb](https://www.omdbapi.com/apikey.aspx)).

## Budowanie

```bash
cd Jellyfin.Plugin.ImdbRenamer
dotnet build -c Release
```

Powstały plik `bin/Release/net8.0/Jellyfin.Plugin.ImdbRenamer.dll` (razem z zależnościami
z folderu `bin/Release/net8.0/`) skopiuj do folderu wtyczek Jellyfin, np.:

```
/var/lib/jellyfin/plugins/ImdbRenamer/
```

Restart serwera Jellyfin.

## Konfiguracja

W panelu administratora: **Dashboard → Plugins → IMDb Renamer**:

- Klucz API TMDb (wymagany),
- Klucz API OMDb (opcjonalny, fallback),
- Czy nadpisywać istniejące IMDb Id,
- Czy faktycznie zmieniać nazwy plików (można też uruchomić w trybie "tylko podgląd/log").

## Uruchamianie

Zadanie zaplanowane (**Dashboard → Scheduled Tasks → Uzupełnij IMDb ID i zmień nazwy plików**)
można uruchomić ręcznie lub ustawić harmonogram (domyślnie wyłączone, uruchamiane ręcznie).

## Publikacja na GitHub

To repozytorium jest gotowe do wypchnięcia na GitHub. Ponieważ agent nie ma (i nie powinien mieć)
Twoich danych logowania, zrób to samodzielnie, np.:

```bash
cd jellyfin-plugin-imdb-renamer
git init
git add .
git commit -m "Initial commit: Jellyfin IMDb Renamer plugin"
git branch -M main
git remote add origin git@github.com:<twoj-user>/jellyfin-plugin-imdb-renamer.git
git push -u origin main
```

Jeśli chcesz, żeby wtyczka pojawiała się w Twoim własnym repozytorium wtyczek Jellyfin
(dodawanym w Dashboard → Plugins → Repositories), plik [manifest.json](manifest.json)
zawiera szablon wpisu — po zbudowaniu wersji zip trzeba uzupełnić `checksum`, `sourceUrl`
i wystawić go pod publicznym URL (np. przez GitHub Pages albo raw.githubusercontent.com).
