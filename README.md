<div align="center">

# AllRename

**Renommeur multimédia intelligent pour Windows**

Simulation Dry-Run · TMDB API · Plex · qBittorrent · Transmission · Rollback JSON

[![Version](https://img.shields.io/badge/version-1.1.0-blue?style=flat-square)](https://github.com/heiphaistos44-crypto/AllRename/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey?style=flat-square)](https://github.com/heiphaistos44-crypto/AllRename/releases)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)

</div>

---

## Présentation

**AllRename** est une application de bureau Windows qui renomme automatiquement vos fichiers multimédia (films et séries) en interrogeant l'API TMDB pour obtenir les titres officiels, les années de sortie et la numérotation des épisodes.

Le principe fondamental est la **simulation Dry-Run** : l'application vous montre exactement ce qui va se passer *avant* d'écrire quoi que ce soit sur le disque. Vous validez, puis vous appliquez.

L'intégration avec **qBittorrent** et **Transmission** permet de renommer les fichiers *pendant* le téléchargement, afin que Plex les détecte correctement dès la fin du download.

---

## Fonctionnalités

### Moteur de renommage
- **Simulation Dry-Run obligatoire** — le bouton "Renommer" est désactivé tant que la simulation n'est pas révisée
- **Parsing lexical** — suppression automatique des tags parasites (`1080p`, `MULTI`, `TRUEFRENCH`, `x264`, `WEBRip`, `BluRay`, groupes de release…)
- **Détection épisodes** — formats `S01E01`, `1x01`, multi-épisodes
- **Traitement par lots** — 20 fichiers en parallèle, supporte des milliers de fichiers sans crash
- **Code couleur des statuts** :
  - 🟢 **Matched** — correspondance TMDB sûre (confiance ≥ 80 %)
  - 🟠 **Partial** — correspondance partielle, à vérifier
  - 🔴 **NotFound** — aucun résultat API
- **Rollback (Ctrl+Z)** — annulation complète de la dernière session de renommage via un fichier JSON temporaire
- **Renommage sous-titres automatique** — `.srt`, `.ass`, `.sub`, `.ssa`, `.vtt`, `.idx` renommés en même temps que la vidéo (variantes langue `.fr.srt`, `.en.srt` incluses)

### Métadonnées
- **TMDB API** (gratuite) — titres officiels FR, années, numérotation épisodes
- **Cache mémoire 24h** — une saison de 24 épisodes = 1 seule requête TMDB
- **Plex API locale** — croisement de données pour un nommage parfaitement compatible avec vos agents Plex
- **Format de sortie** :
  - Films : `Titre (Année).ext` → ex. `Avatar Le Dernier Maitre De L Air (2025).mkv`
  - Séries : `Titre (Année) - SXXEYY.ext` → ex. `Breaking Bad (2008) - S01E01.mkv`

### Intégration torrents
- **qBittorrent** (Web API v2) — renommage des fichiers via l'API native
- **Transmission** (RPC JSON) — renommage via `torrent-rename-path`
- Renommage **pendant le téléchargement** — Plex détecte correctement dès la fin du DL
- Panneau splitté : liste des torrents actifs (gauche) + fichiers avec suggestions TMDB (droite)

### Qualité
- Architecture **MVVM** stricte (CommunityToolkit.Mvvm)
- Toutes les opérations I/O en **async/await** — UI jamais bloquée
- **Logs locaux** avec rotation à 1 MB → `%LocalAppData%\AllRename\.logs\`
- **Exécutable unique** — aucune installation du runtime .NET requise

---

## Téléchargement

| Fichier | Description | Taille |
|---------|-------------|--------|
| [`AllRename_v1.1.0_Portable.exe`](https://github.com/heiphaistos44-crypto/AllRename/releases/latest) | Exécutable autonome — double-clic suffit | ~171 MB |
| [`AllRename_v1.1.0_Setup.exe`](https://github.com/heiphaistos44-crypto/AllRename/releases/latest) | Installateur Windows avec raccourcis | ~80 MB |

> **Pourquoi 171 MB pour le portable ?**
> Le runtime .NET 8 est entièrement embarqué — aucune dépendance à installer sur la machine cible.

---

## Prérequis

- **Windows 10** (1903+) ou **Windows 11**
- **Clé API TMDB gratuite** — [https://www.themoviedb.org/settings/api](https://www.themoviedb.org/settings/api) (inscription gratuite, clé générée en 2 minutes)
- **qBittorrent ≥ 4.3.3** avec l'interface Web activée *(optionnel)*
- **Transmission** avec l'interface RPC activée *(optionnel)*
- **Plex Media Server** local *(optionnel)*

---

## Configuration

### 1. Clé API TMDB (obligatoire pour le renommage)

1. Créer un compte sur [themoviedb.org](https://www.themoviedb.org)
2. Aller dans **Paramètres → API → Créer une clé** (type : Développeur)
3. Copier la **clé API v3** (chaîne de 32 caractères hexadécimaux)
4. Dans AllRename : champ **"Clé TMDB"** → coller → **Enregistrer**

> La clé est chiffrée via DPAPI (Windows) et persistée dans `%LocalAppData%\AllRename\settings.json`. Elle n'est jamais committée dans le code.

### 2. Plex (optionnel)

| Paramètre | Valeur |
|-----------|--------|
| URL | `http://127.0.0.1:32400` (local) ou IP LAN |
| Token | Paramètres Plex → **Accès distant** → afficher le token |

### 3. qBittorrent

1. qBittorrent → **Outils → Préférences → Interface Web**
2. Activer l'interface Web — port par défaut : `8080`
3. Définir un nom d'utilisateur et mot de passe
4. Dans AllRename onglet **Torrents** :
   - Client : `qBittorrent`
   - URL : `http://localhost:8080`
   - Identifiants → **Connecter**

### 4. Transmission

1. Transmission → **Édition → Préférences → Accès distant**
2. Activer l'accès distant — port par défaut : `9091`
3. Dans AllRename onglet **Torrents** :
   - Client : `Transmission`
   - URL : `http://localhost:9091`
   - Identifiants *(si authentification activée)* → **Connecter**

---

## Utilisation

### Onglet "Fichiers locaux"

```
1. Cliquer "Parcourir" → sélectionner le dossier multimédia
2. Cliquer "Simuler" → l'application scanne et interroge TMDB
3. Réviser le DataGrid :
   - Modifier un nom directement dans la cellule "Nouveau nom"
   - Décocher les fichiers à exclure
4. Cliquer "Renommer les fichiers sélectionnés" (activé après simulation)
5. En cas d'erreur : Ctrl+Z → rollback complet
```

### Onglet "Torrents"

```
1. Sélectionner le client (qBittorrent / Transmission)
2. Renseigner URL + identifiants → "Connecter"
3. Sélectionner un torrent en cours de téléchargement
4. Cliquer "Analyser TMDB" → suggestions dans la colonne "Nom suggéré"
5. Vérifier / modifier les noms
6. Cliquer "Appliquer renommage dans le client"
   → Le client renomme le fichier sur disque en temps réel
   → Plex détecte le bon nom à la fin du téléchargement
```

---

## Format de sortie

| Type | Format | Exemple |
|------|--------|---------|
| Film | `Titre (Année).ext` | `Inception (2010).mkv` |
| Série | `Titre (Année) - SXXEYY.ext` | `Breaking Bad (2008) - S01E01.mkv` |
| Anime | `Titre (Année) - SXXEYY.ext` | `Attack On Titan (2013) - S01E01.mkv` |

Tags supprimés automatiquement : `1080p` `720p` `4K` `MULTI` `TRUEFRENCH` `FRENCH` `VOSTFR` `x264` `x265` `HEVC` `BluRay` `WEBRip` `WEB-DL` `DVDRip` `YIFY` `RARBG` `DD5.1` `DTS` `AAC` `REMUX` et bien d'autres.

---

## Architecture

```
AllRename/
├── Models/                         Entités de données
│   ├── FileEntry.cs                Fichier à renommer (INotifyPropertyChanged)
│   ├── MediaInfo.cs                Métadonnées TMDB/Plex
│   ├── TorrentEntry.cs             Torrent + fichiers
│   ├── RollbackEntry.cs            Batch d'annulation JSON
│   └── RenameResult.cs             Résultat d'exécution
├── Services/
│   ├── Interfaces/                 6 contrats d'interface
│   ├── ParserService.cs            Nettoyage regex + détection S/E
│   ├── RenamerCore.cs              Simulation (Dry-Run) + exécution par lots
│   ├── TmdbService.cs              API TMDB fr-FR + cache mémoire 24h
│   ├── PlexService.cs              API Plex locale
│   ├── QBittorrentService.cs       qBittorrent Web API v2
│   ├── TransmissionService.cs      Transmission RPC JSON
│   ├── RollbackService.cs          Sérialisation JSON + annulation
│   ├── FileScanner.cs              Scan récursif async
│   └── LogService.cs               Logs locaux + rotation 1 MB
├── ViewModels/                     Pattern MVVM (CommunityToolkit.Mvvm)
│   ├── MainViewModel.cs            Orchestrateur principal
│   ├── SettingsViewModel.cs        Configuration API
│   └── TorrentViewModel.cs         Intégration clients torrent
├── Views/
│   └── MainWindow.xaml             TabControl 2 onglets
├── Converters/                     StatusToColor, BoolToVisibility
└── Resources/Styles.xaml           Styles WPF globaux
```

---

## Build depuis les sources

### Prérequis build
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) *(pour l'installateur uniquement)*
- Git

```bat
git clone https://github.com/heiphaistos44-crypto/AllRename.git
cd AllRename

# Portable (.exe autonome)
build.bat portable

# Installateur (requiert Inno Setup 6)
build.bat installer

# Les deux + release GitHub
build.bat all
```

Sorties :
- Portable → `.\publish\AllRename.exe`
- Installateur → `.\installer\output\AllRename_v1.1.0_Setup.exe`

---

## Roadmap

- [x] Persistance de la clé TMDB entre les sessions (chiffrement DPAPI) — v1.1.0
- [x] Support des sous-titres (`.srt`, `.ass`) — renommage synchronisé avec la vidéo — v1.1.0
- [ ] Intégration Tautulli pour les statistiques de renommage
- [ ] Thème sombre
- [ ] Rapport HTML exportable après renommage
- [ ] Support anime — détection AniDB en fallback TMDB

---

## Versions

| Version | Date | Changements |
|---------|------|-------------|
| 1.1.0 | 2026-05-27 | Audit sécurité — path traversal, DPAPI settings, sous-titres auto, bugfixes |
| 1.0.0 | 2026-05-26 | Release initiale — fichiers locaux, TMDB, Plex, qBittorrent, Transmission |

---

## Licence

MIT — voir [LICENSE](LICENSE)
