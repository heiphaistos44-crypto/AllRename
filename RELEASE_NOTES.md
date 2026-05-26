## AllRename v1.0.0 — Release initiale

### Nouveautés
- **Renommage local** — simulation Dry-Run + exécution par lots (20 fichiers/batch)
- **TMDB API** — titres officiels FR, années, détection films/séries, cache 24h
- **Plex** — croisement de métadonnées avec le serveur Plex local
- **qBittorrent** — renommage de fichiers pendant le téléchargement via Web API v2
- **Transmission** — renommage via RPC JSON (`torrent-rename-path`)
- **Rollback** — annulation complète via Ctrl+Z (fichier JSON temporaire)
- **Parsing lexical** — suppression automatique de 50+ tags parasites (1080p, MULTI, TRUEFRENCH…)
- **Exécutable unique** — .NET 8 runtime embarqué, aucune installation requise

### Téléchargements
| Fichier | Description |
|---------|-------------|
| `AllRename_v1.0.0_Portable.exe` | Exécutable autonome (~171 MB) |
| `AllRename_v1.0.0_Setup.exe` | Installateur Windows avec raccourcis (~80 MB) |

### Configuration requise
- Windows 10 (1903+) ou Windows 11 — 64 bits
- Clé API TMDB gratuite : https://www.themoviedb.org/settings/api
