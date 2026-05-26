## AllRename v1.1.0 — Audit sécurité & nouvelles fonctionnalités

### Sécurité (critiques)
- **Path traversal bloqué** — `RenamerCore.ExecuteAsync` valide que le fichier cible reste dans le dossier source avant tout `File.Move`
- **Validation noms fichiers** — `QBittorrentService` et `TransmissionService` rejettent les noms contenant des caractères invalides (`Path.GetInvalidFileNameChars`)
- **Validation URL** — les URL qBittorrent et Transmission doivent être `http://` ou `https://` (schéma vérifié avant connexion)

### Corrections de bugs
- **`FileEntry.NewPath`** — gardé contre un `NewName` vide (retourne `SourcePath` au lieu de provoquer une `IOException`)
- **`QBittorrentService.GetFilesAsync`** — l'exception n'était plus avalée silencieusement, maintenant loguée
- **`LogService.PurgeOldLogsAsync`** — race condition corrigée : `PurgeOldLogs` est maintenant async et acquiert le `SemaphoreSlim`
- **Installateur Inno Setup** — dossier de désinstallation corrigé : `{localappdata}\AllRename` (était `{userappdata}`)
- **QuickLaunch Inno Setup** — tâche QuickLaunch supprimée (`OnlyBelowVersion: 6.1` la rendait invisible sur Windows 10/11)

### Nouvelles fonctionnalités
- **Persistance des paramètres (DPAPI)** — la clé TMDB et le token Plex sont chiffrés via `ProtectedData.Protect` (DPAPI, scope `CurrentUser`) et sauvegardés dans `%LocalAppData%\AllRename\settings.json`. Les URLs et identifiants torrents sont aussi persistés.
- **Renommage automatique des sous-titres** — après chaque renommage vidéo, les fichiers `.srt`, `.ass`, `.sub`, `.ssa`, `.vtt`, `.idx` correspondants (y compris variantes langue `.fr.srt`, `.en.srt`) sont renommés automatiquement. Les opérations sous-titres sont incluses dans le rollback JSON.
- **Disposal correct des services** — `TmdbService`, `PlexService`, `QBittorrentService` et `TransmissionService` sont maintenant disposés proprement dans `App.OnExit`

### Téléchargements
| Fichier | Description |
|---------|-------------|
| `AllRename_v1.1.0_Portable.exe` | Exécutable autonome (~171 MB) |
| `AllRename_v1.1.0_Setup.exe` | Installateur Windows avec raccourcis (~80 MB) |

### Configuration requise
- Windows 10 (1903+) ou Windows 11 — 64 bits
- Clé API TMDB gratuite : https://www.themoviedb.org/settings/api

---

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
