using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class QBittorrentService : ITorrentService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private string _baseUrl = string.Empty;

    public bool IsConnected { get; private set; }

    public QBittorrentService()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> ConnectAsync(string url, string username, string password, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBittorrent URL invalide : {url}");
            return false;
        }
        _baseUrl = url.TrimEnd('/');
        IsConnected = false;
        try
        {
            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });
            var response = await _http.PostAsync($"{_baseUrl}/api/v2/auth/login", body, ct);
            string text = (await response.Content.ReadAsStringAsync(ct)).Trim();
            IsConnected = text == "Ok.";
            await LogService.WriteAsync(LogLevel.Info, $"qBittorrent connexion: {(IsConnected ? "OK" : "Échec")} — {_baseUrl}");
            return IsConnected;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBittorrent connect: {ex.Message}");
            return false;
        }
    }

    public async Task<IReadOnlyList<TorrentEntry>> GetTorrentsAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return Array.Empty<TorrentEntry>();
        try
        {
            var items = await _http.GetFromJsonAsync<List<QbTorrent>>($"{_baseUrl}/api/v2/torrents/info", JsonOpts, ct);
            if (items == null) return Array.Empty<TorrentEntry>();

            var result = new List<TorrentEntry>(items.Count);
            foreach (var item in items)
            {
                var entry = new TorrentEntry
                {
                    Hash = item.Hash ?? string.Empty,
                    Name = item.Name ?? string.Empty,
                    SavePath = item.SavePath ?? string.Empty,
                    Progress = item.Progress,
                    TotalSize = item.Size,
                    State = MapState(item.State)
                };

                // Charger les fichiers uniquement pour les torrents actifs (pas complétés)
                if (entry.State is TorrentState.Downloading or TorrentState.Paused or TorrentState.Queued)
                    entry.Files = await GetFilesAsync(entry.Hash, ct);

                result.Add(entry);
            }
            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBittorrent GetTorrents: {ex.Message}");
            return Array.Empty<TorrentEntry>();
        }
    }

    private async Task<List<TorrentFileEntry>> GetFilesAsync(string hash, CancellationToken ct)
    {
        try
        {
            var files = await _http.GetFromJsonAsync<List<QbFile>>(
                $"{_baseUrl}/api/v2/torrents/files?hash={hash}", JsonOpts, ct);

            return files?.Select((f, i) => new TorrentFileEntry
            {
                Index = f.Index >= 0 ? f.Index : i,
                OriginalName = Path.GetFileName(f.Name ?? string.Empty),
                RelativePath = f.Name ?? string.Empty,
                Size = f.Size,
                Progress = f.Progress
            }).ToList() ?? new();
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBittorrent GetFiles '{hash}': {ex.Message}");
            return new();
        }
    }

    public async Task<bool> RenameFileAsync(TorrentEntry torrent, TorrentFileEntry file, string newName, CancellationToken ct = default)
    {
        if (!IsConnected) return false;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBit RenameFile : nom invalide '{newName}'");
            return false;
        }
        try
        {
            // Construire le nouveau chemin relatif (garder le dossier parent)
            string dir = Path.GetDirectoryName(file.RelativePath)?.Replace('\\', '/') ?? string.Empty;
            string newPath = string.IsNullOrEmpty(dir) ? newName : $"{dir}/{newName}";

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hash", torrent.Hash),
                new KeyValuePair<string, string>("oldPath", file.RelativePath),
                new KeyValuePair<string, string>("newPath", newPath)
            });

            var response = await _http.PostAsync($"{_baseUrl}/api/v2/torrents/renameFile", body, ct);
            bool ok = response.IsSuccessStatusCode;
            await LogService.WriteAsync(ok ? LogLevel.Info : LogLevel.Error,
                $"qBit rename '{file.RelativePath}' → '{newPath}': {(ok ? "OK" : response.StatusCode)}");
            return ok;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"qBit RenameFile: {ex.Message}");
            return false;
        }
    }

    private static TorrentState MapState(string? state) => state switch
    {
        "downloading" or "forcedDL"                => TorrentState.Downloading,
        "uploading" or "stalledUP" or "forcedUP"   => TorrentState.Uploading,
        "pausedDL" or "pausedUP"                   => TorrentState.Paused,
        "queuedDL" or "queuedUP"                   => TorrentState.Queued,
        "checkingDL" or "checkingUP" or "checkingResumeData" => TorrentState.Checking,
        "stalledDL"                                => TorrentState.Downloading,
        "missingFiles" or "error"                  => TorrentState.Error,
        _                                          => TorrentState.Completed
    };

    public void Dispose() => _http.Dispose();

    // DTOs internes
    private sealed record QbTorrent(
        string? Hash, string? Name, string? SavePath,
        string? State, double Progress, long Size);

    private sealed record QbFile(
        int Index, string? Name, long Size, double Progress);
}
