using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface IPlexService
{
    bool IsConfigured { get; }
    Task<MediaInfo?> LookupAsync(string title, MediaType type, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
