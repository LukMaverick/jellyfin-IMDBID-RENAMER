# Jellyfin Plugin: IMDb Renamer

Wtyczka do Jellyfin (aktualna wersja: **1.0.0.7**), która automatycznie:

1. Wykrywa **nowe pliki dodane do biblioteki** (nasłuch zdarzeń Jellyfin) i przetwarza je
   bez czekania na harmonogram, a dodatkowo udostępnia zadanie do jednorazowego
   przebiegu po całej bibliotece,
2. Parsuje tytuł i rok bezpośrednio z **nazwy pliku** (nie ufa możliwie błędnie
   rozpoznanej nazwie z Jellyfin),
3. Wyszukuje film/serial w **TMDb** (klucz API v3 lub token v4/Bearer), a jeśli się
   nie uda — w **OMDb**, z walidacją roku dopasowania (chroni przed pomyleniem
   sequeli/tytułów o tej samej nazwie),
4. Uzupełnia pole **IMDb Id** w metadanych pozycji w bibliotece Jellyfin,
5. Zmienia nazwę pliku wideo na czysty format `Tytuł (Rok).ext` (bez zmiany folderu),
   zamieniając przy tym znaki niedozwolone w Windows (np. dwukropek), żeby uniknąć
   "mangled names" (skróconych nazw 8.3) przy udostępnianiu przez Sambę.

## Wymagania

- Jellyfin Server **12.1.x** (target framework `net10.0`). Jeśli masz inną wersję serwera,
  zmień wersje pakietów `Jellyfin.Controller` / `Jellyfin.Model` w
  [Jellyfin.Plugin.ImdbRenamer.csproj](Jellyfin.Plugin.ImdbRenamer/Jellyfin.Plugin.ImdbRenamer.csproj)
  na wersję zgodną z Twoim serwerem (musi się zgadzać z numerem wersji Jellyfin).
- .NET 10 SDK do budowania.
- Darmowy klucz API z [TMDb](https://www.themoviedb.org/settings/api) (v3 "API Key" albo
  v4 "API Read Access Token" — oba formaty są obsługiwane) i opcjonalnie z
  [OMDb](https://www.omdbapi.com/apikey.aspx).

## Budowanie

```bash
cd Jellyfin.Plugin.ImdbRenamer
dotnet build -c Release
```

Powstały plik `bin/Release/net10.0/Jellyfin.Plugin.ImdbRenamer.dll` (razem z zależnościami
z folderu `bin/Release/net10.0/`) skopiuj do folderu wtyczek Jellyfin, np.:

```
/var/lib/jellyfin/plugins/IMDb Renamer_1.0.0.7/
```

Restart serwera Jellyfin.

## Konfiguracja

W panelu administratora: **Dashboard → Plugins → IMDb Renamer**:

- Klucz API TMDb (wymagany),
- Klucz API OMDb (opcjonalny, fallback),
- Czy nadpisywać istniejące IMDb Id,
- Czy faktycznie zmieniać nazwy plików (można też uruchomić w trybie "tylko podgląd/log").

## Uruchamianie

- **Automatycznie**: nowe pliki wykryte przez Jellyfin (po skanie biblioteki) są przetwarzane
  samoczynnie po ok. 15 sekundach od dodania (bez potrzeby uruchamiania czegokolwiek ręcznie).
- **Ręcznie / dla całej biblioteki**: zadanie zaplanowane
  (**Dashboard → Scheduled Tasks → Uzupełnij IMDb ID i zmień nazwy plików**)
  można uruchomić ręcznie lub ustawić własny harmonogram (domyślnie brak automatycznego
  wyzwalacza czasowego).

## Instalacja z repozytorium wtyczek

W Jellyfin: **Dashboard → Plugins → Repositories → New Repository**, podaj:

```
https://raw.githubusercontent.com/LukMaverick/jellyfin-IMDBID-RENAMER/main/manifest.json
```

Następnie w katalogu wtyczek (**Catalog**) znajdziesz "IMDb Renamer" do zainstalowania.
Pełna historia wersji: [Releases](https://github.com/LukMaverick/jellyfin-IMDBID-RENAMER/releases).
i wystawić go pod publicznym URL (np. przez GitHub Pages albo raw.githubusercontent.com).
