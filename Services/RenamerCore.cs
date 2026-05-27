using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class RenamerCore : IRenamerCore
{
    private const int BatchSize = 20;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;
    private const int MaxPathLength = 250; // Seuil avant préfixe \\?\

    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".sub", ".ssa", ".vtt", ".idx" };

    private readonly IParserService _parser;
    private readonly ITmdbService _tmdb;
    private readonly IPlexService _plex;

    public RenamerCore(IParserService parser, ITmdbService tmdb, IPlexService plex)
    {
        _parser = parser;
        _tmdb = tmdb;
        _plex = plex;
    }

    // ──────────────────────────────────────────────────────────────
    //  SIMULATION (Dry Run)
    // ──────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<FileEntry>> SimulateAsync(
        IEnumerable<string> filePaths,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var paths = filePaths.ToList();
        var results = new List<FileEntry>(paths.Count);
        int processed = 0;

        foreach (var batch in paths.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            // Parallèle dans le batch ; le cache TMDB évite les doublons (ex: 24 épisodes = 1 requête)
            var tasks = batch.Select(path => ProcessFileAsync(path, ct));
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);

            processed += batch.Length;
            progress?.Report((processed, paths.Count));
        }

        await LogService.WriteAsync(LogLevel.Info,
            $"Simulation : {results.Count} fichiers | " +
            $"Matched={results.Count(r => r.Status == MatchStatus.Matched)} | " +
            $"Partial={results.Count(r => r.Status == MatchStatus.Partial)} | " +
            $"NotFound={results.Count(r => r.Status == MatchStatus.NotFound)}");

        return results.AsReadOnly();
    }

    private async Task<FileEntry> ProcessFileAsync(string filePath, CancellationToken ct)
    {
        var entry = new FileEntry { SourcePath = filePath };
        try
        {
            var parsed = _parser.Parse(Path.GetFileName(filePath));

            if (string.IsNullOrWhiteSpace(parsed.CleanTitle))
            {
                entry.Status = MatchStatus.NotFound;
                entry.ErrorMessage = "Titre non extractible";
                return entry;
            }

            MediaInfo? info = null;

            if (_tmdb.IsConfigured)
            {
                info = parsed.DetectedType == MediaType.Movie
                    ? await _tmdb.SearchMovieAsync(parsed.CleanTitle, parsed.Year, ct)
                    : await _tmdb.SearchSeriesAsync(parsed.CleanTitle, parsed.Year, ct);
            }

            if (info == null && _plex.IsConfigured)
                info = await _plex.LookupAsync(parsed.CleanTitle, parsed.DetectedType, ct);

            if (info == null)
            {
                entry.Status = MatchStatus.NotFound;
                entry.ErrorMessage = _tmdb.IsConfigured ? "Aucun résultat API" : "Clé TMDB non configurée";
                return entry;
            }

            if (parsed.Season.HasValue) info.Season = parsed.Season;
            if (parsed.Episode.HasValue) info.Episode = parsed.Episode;

            entry.Media = info;
            entry.NewName = _parser.BuildTargetName(info, entry.Extension);
            entry.Status = info.MatchConfidence >= 0.8 ? MatchStatus.Matched : MatchStatus.Partial;
        }
        catch (Exception ex)
        {
            entry.Status = MatchStatus.Error;
            entry.ErrorMessage = ex.Message;
            await LogService.WriteAsync(LogLevel.Error, $"ProcessFile '{filePath}': {ex.Message}");
        }

        return entry;
    }

    // ──────────────────────────────────────────────────────────────
    //  EXÉCUTION
    // ──────────────────────────────────────────────────────────────
    public async Task<RollbackBatch> ExecuteAsync(
        IEnumerable<FileEntry> entries,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var toRename = entries
            .Where(e => e.IsIncluded && e.Status is MatchStatus.Matched or MatchStatus.Partial)
            .ToList();

        // ── Pre-flight : détection collisions inter-batch ──────────
        // Fix Bug#2 : deux fichiers → même NewPath → IOException non gérée.
        // On résout avant d'écrire quoi que ce soit sur le disque.
        await ResolveCollisionsAsync(toRename);

        var batch = new RollbackBatch();
        int done = 0;

        foreach (var entry in toRename)
        {
            ct.ThrowIfCancellationRequested();

            // Fix Bug#6 : comparaison OrdinalIgnoreCase (Windows est case-insensitive)
            if (string.Equals(entry.SourcePath, entry.NewPath, StringComparison.OrdinalIgnoreCase))
            {
                done++;
                progress?.Report((done, toRename.Count));
                continue;
            }

            // S1 — path traversal guard
            string baseDir = entry.SourceDirectory;
            if (!IsPathSafe(baseDir, entry.NewPath))
            {
                entry.Status = MatchStatus.Error;
                entry.ErrorMessage = "Chemin cible hors du dossier source — opération refusée.";
                await LogService.WriteAsync(LogLevel.Error, $"Path traversal bloqué : '{entry.NewPath}'");
                done++;
                progress?.Report((done, toRename.Count));
                continue;
            }

            // S2 — long path guard (Fix Bug#10)
            string safeSrc = ToLongPath(entry.SourcePath);
            string safeDst = ToLongPath(entry.NewPath);

            try
            {
                // Fix Bug#5 : retry sur file lock (HRESULT 0x80070020)
                await TryMoveWithRetryAsync(safeSrc, safeDst);
                batch.Entries.Add(new RollbackEntry
                {
                    OriginalPath = entry.SourcePath,
                    NewPath = entry.NewPath
                });
                await LogService.WriteAsync(LogLevel.Info, $"Renommé : '{entry.SourcePath}' → '{entry.NewPath}'");

                // Sous-titres associés
                await RenameMatchingSubtitlesAsync(entry.SourcePath, entry.NewPath, batch);
            }
            catch (Exception ex)
            {
                entry.Status = MatchStatus.Error;
                entry.ErrorMessage = ex.Message;
                await LogService.WriteAsync(LogLevel.Error, $"Execute '{entry.SourcePath}': {ex.Message}");
            }

            done++;
            progress?.Report((done, toRename.Count));
        }

        return batch;
    }

    // ──────────────────────────────────────────────────────────────
    //  HELPERS — Sécurité
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fix Bug#2 : Résout les collisions NewPath au sein du batch AVANT toute écriture disque.
    /// Stratégie : le premier fichier garde le nom ciblé, les suivants reçoivent _1, _2, etc.
    /// </summary>
    private static async Task ResolveCollisionsAsync(List<FileEntry> toRename)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in toRename)
        {
            // Collision avec fichier déjà existant sur disque (hors batch)
            string targetPath = entry.NewPath;

            if (seen.Contains(targetPath) || File.Exists(targetPath))
            {
                string dir = entry.SourceDirectory;
                string nameNoExt = Path.GetFileNameWithoutExtension(entry.NewName);
                string ext = Path.GetExtension(entry.NewName);
                int suffix = 1;
                string resolved;

                do
                {
                    resolved = $"{nameNoExt}_{suffix++}{ext}";
                    targetPath = Path.Combine(dir, resolved);
                }
                while (seen.Contains(targetPath) || File.Exists(targetPath));

                await LogService.WriteAsync(LogLevel.Warn,
                    $"Collision résolue : '{entry.NewPath}' → '{targetPath}'");
                entry.NewName = resolved;
            }

            seen.Add(entry.NewPath);
        }
    }

    /// <summary>
    /// Fix Bug#5 : 3 tentatives avec délai sur IOException (verrou fichier).
    /// </summary>
    private static async Task TryMoveWithRetryAsync(string source, string dest)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                File.Move(source, dest, overwrite: false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                await LogService.WriteAsync(LogLevel.Warn,
                    $"Fichier verrouillé, tentative {attempt}/{MaxRetries} : '{source}'");
                await Task.Delay(RetryDelayMs);
            }
            // Dernière tentative → laisse l'exception remonter
        }
        File.Move(source, dest, overwrite: false);
    }

    private static bool IsPathSafe(string baseDir, string targetPath)
    {
        string fullBase = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        string fullTarget = Path.GetFullPath(targetPath);
        return fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fix Bug#10 : ajoute le préfixe \\?\ sur Windows pour les chemins > 250 chars.
    /// Permet de dépasser la limite MAX_PATH (260) sans modifier le registre.
    /// </summary>
    private static string ToLongPath(string path)
    {
        if (path.Length <= MaxPathLength) return path;
        if (path.StartsWith(@"\\?\")) return path;
        // UNC déjà \\server\share → \\?\UNC\server\share
        if (path.StartsWith(@"\\"))
            return @"\\?\UNC\" + path[2..];
        return @"\\?\" + path;
    }

    // ──────────────────────────────────────────────────────────────
    //  SOUS-TITRES
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fix Bug#4 + Bug#7 : log des erreurs + bounds check sur Substring.
    /// </summary>
    private static async Task RenameMatchingSubtitlesAsync(
        string oldVideoPath, string newVideoPath, RollbackBatch batch)
    {
        string dir = Path.GetDirectoryName(oldVideoPath) ?? string.Empty;
        string videoBase = Path.GetFileNameWithoutExtension(oldVideoPath);
        string newBase = Path.GetFileNameWithoutExtension(newVideoPath);

        foreach (var ext in SubtitleExtensions)
        {
            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(dir, $"{videoBase}*{ext}");
            }
            catch (Exception ex)
            {
                await LogService.WriteAsync(LogLevel.Warn,
                    $"Scan sous-titres impossible dans '{dir}': {ex.Message}");
                continue;
            }

            foreach (var sub in candidates)
            {
                string subName = Path.GetFileName(sub);

                // Fix Bug#7 : guard bounds avant Substring
                int suffixLen = subName.Length - videoBase.Length - ext.Length;
                if (suffixLen < 0)
                {
                    await LogService.WriteAsync(LogLevel.Warn,
                        $"Sous-titre ignoré (nom incohérent) : '{sub}'");
                    continue;
                }

                string suffix = subName.Substring(videoBase.Length, suffixLen);
                string newSubName = newBase + suffix + ext;
                string newSubPath = Path.Combine(dir, newSubName);

                try
                {
                    if (!File.Exists(newSubPath))
                    {
                        File.Move(sub, newSubPath, overwrite: false);
                        batch.Entries.Add(new RollbackEntry { OriginalPath = sub, NewPath = newSubPath });
                        await LogService.WriteAsync(LogLevel.Info,
                            $"Sous-titre renommé : '{Path.GetFileName(sub)}' → '{newSubName}'");
                    }
                }
                catch (Exception ex)
                {
                    // Fix Bug#4 : plus de catch silencieux
                    await LogService.WriteAsync(LogLevel.Warn,
                        $"Sous-titre non renommé '{sub}': {ex.Message}");
                }
            }
        }
    }
}
