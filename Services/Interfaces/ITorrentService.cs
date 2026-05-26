using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface ITorrentService
{
    bool IsConnected { get; }
    Task<bool> ConnectAsync(string url, string username, string password, CancellationToken ct = default);
    Task<IReadOnlyList<TorrentEntry>> GetTorrentsAsync(CancellationToken ct = default);
    Task<bool> RenameFileAsync(TorrentEntry torrent, TorrentFileEntry file, string newName, CancellationToken ct = default);
}
