## AllRename v1.2.0 — Audit robustesse & moteur métadonnées

### Corrections critiques
- **Bug#1 — Corruption emoji/Unicode** (`ParserService.CapitalizeWords`) — `w[0]`/`w[1..]` opéraient sur des surrogates UTF-16 bruts. Un nom de fichier contenant un emoji (🎬, 📺…) ou un caractère CJK Extension provoquait une corruption silencieuse du titre ou un crash. Corrigé par détection du surrogate pair avant capitalisation.
- **Bug#2 — Collision inter-batch** (`RenamerCore.ExecuteAsync`) — deux fichiers mappés sur le même `NewPath` levaient une `IOException` non gérée. Ajout d'une passe pre-flight `ResolveCollisionsAsync` qui détecte et résout les doublons (suffixe `_1`, `_2`…) AVANT toute écriture disque.
- **Bug#3 — Rollback LIFO incorrect** (`RollbackService.RollbackAsync`) — le rollback itérait en ordre chronologique. Sur une chaîne A→B→C, il tentait de défaire A←B alors que le fichier s'appellait déjà C. Corrigé : `Enumerable.Reverse()` — LIFO garanti.

### Corrections hautes priorité
- **Bug#4 — catch silencieux sous-titres** — `catch { }` avalait toutes les erreurs de renommage des sous-titres sans log. Remplacé par log `Warn` systématique.
- **Bug#5 — Aucun retry sur verrou fichier** — `File.Move` sur un fichier ouvert par un autre process levait `IOException` immédiatement. Ajout d'un mécanisme retry 3×500ms avant d'échouer proprement.
- **Bug#6 — Comparaison chemin case-sensitive** — `entry.SourcePath == entry.NewPath` ignorait la casse Windows. Corrigé : `StringComparison.OrdinalIgnoreCase`.
- **Bug#7 — `Substring` sans bounds check** — `subName.Substring(videoBase.Length, subName.Length - videoBase.Length - ext.Length)` pouvait produire un count négatif sur un fichier sous-titre malformé. Guard ajouté.

### Corrections moyennes priorité
- **Bug#8 — ReDoS `GroupPattern`** — regex `.*?` imbriqué sans timeout. Ajout `TimeSpan.FromMilliseconds(100)` + catch `RegexMatchTimeoutException`.
- **Bug#9 — `RotateIfNeededAsync` synchrone** — `File.Move` bloquait le thread du `SemaphoreSlim`. Signature corrigée (vraie méthode `async`).
- **Bug#10 — Chemins longs Windows** — aucun préfixe `\\?\` pour dépasser MAX_PATH (260 chars). Helper `ToLongPath()` ajouté dans `RenamerCore`.

### Nouvelles fonctionnalités
- **Moteur métadonnées EXIF/ID3** — architecture `MetadataService` + `MetadataResult` + `IMetadataService`. Supporte photos (EXIF : date, caméra, GPS) et audio (ID3 : titre, artiste, album, piste). Patterns configurables (`{date:yyyy-MM-dd} {camera}`, `{track:D2} - {artist} - {title}`). Activer en décommentant `MetadataExtractor` + `TagLibSharp` dans le csproj.

### Téléchargements
| Fichier | Description |
|---------|-------------|
| `AllRename_v1.2.0_Portable.exe` | Exécutable autonome (~171 MB) |
| `AllRename_v1.2.0_Setup.exe` | Installateur Windows avec raccourcis (~80 MB) |

---

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
