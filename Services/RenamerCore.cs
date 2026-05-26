using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class RenamerCore : IRenamerCore
{
    private const int BatchSize = 20;

    private readonly IParserService _parser;
    private readonly ITmdbService _tmdb;
    private readonly IPlexService _plex;

    public RenamerCore(IParserService parser, ITmdbService tmdb, IPlexService plex)
    {
        _parser = parser;
        _tmdb = tmdb;
        _plex = plex;
    }

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

            // Injecter numéros S/E depuis le parsing
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

    public async Task<RollbackBatch> ExecuteAsync(
        IEnumerable<FileEntry> entries,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var toRename = entries
            .Where(e => e.IsIncluded && e.Status is MatchStatus.Matched or MatchStatus.Partial)
            .ToList();

        var batch = new RollbackBatch();
        int done = 0;

        foreach (var entry in toRename)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.SourcePath == entry.NewPath)
            {
                done++;
                progress?.Report((done, toRename.Count));
                continue;
            }

            try
            {
                File.Move(entry.SourcePath, entry.NewPath, overwrite: false);
                batch.Entries.Add(new RollbackEntry
                {
                    OriginalPath = entry.SourcePath,
                    NewPath = entry.NewPath
                });
                await LogService.WriteAsync(LogLevel.Info, $"Renommé : '{entry.SourcePath}' → '{entry.NewPath}'");
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
}
