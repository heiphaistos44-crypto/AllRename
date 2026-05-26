using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface ITmdbService
{
    bool IsConfigured { get; }
    Task<MediaInfo?> SearchMovieAsync(string title, int? year = null, CancellationToken ct = default);
    Task<MediaInfo?> SearchSeriesAsync(string title, int? year = null, CancellationToken ct = default);
}
