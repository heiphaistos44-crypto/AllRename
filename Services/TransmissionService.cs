using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class TransmissionService : ITorrentService, IDisposable
{
    private readonly HttpClient _http;
    private string _baseUrl = string.Empty;
    private string? _sessionId;

    public bool IsConnected { get; private set; }

    public TransmissionService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> ConnectAsync(string url, string username, string password, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await LogService.WriteAsync(LogLevel.Error, $"Transmission URL invalide : {url}");
            return false;
        }
        _baseUrl = url.TrimEnd('/');
        IsConnected = false;

        // Configurer Basic Auth si identifiants fournis
        if (!string.IsNullOrWhiteSpace(username))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        try
        {
            // Premier appel → récupère le session-id via 409
            await FetchSessionIdAsync(ct);
            // Vérification avec un appel réel
            var result = await RpcAsync(new { method = "session-get" }, ct);
            IsConnected = result != null;
            await LogService.WriteAsync(LogLevel.Info,
                $"Transmission connexion: {(IsConnected ? "OK" : "Échec")} — {_baseUrl}");
            return IsConnected;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"Transmission connect: {ex.Message}");
            return false;
        }
    }

    private async Task FetchSessionIdAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transmission/rpc")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            if (response.Headers.TryGetValues("X-Transmission-Session-Id", out var values))
                _sessionId = values.FirstOrDefault();
        }
    }

    private async Task<JsonDocument?> RpcAsync(object payload, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transmission/rpc")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            if (_sessionId != null)
                request.Headers.Add("X-Transmission-Session-Id", _sessionId);

            var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                if (response.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
                    _sessionId = vals.FirstOrDefault();
                continue;
            }

            if (!response.IsSuccessStatusCode) return null;
            string body = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(body);
        }
        return null;
    }

    public async Task<IReadOnlyList<TorrentEntry>> GetTorrentsAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return Array.Empty<TorrentEntry>();
        try
        {
            var payload = new
            {
                method = "torrent-get",
                arguments = new
                {
                    fields = new[] { "id", "hashString", "name", "downloadDir", "status", "percentDone", "totalSize", "files" }
                }
            };

            using var doc = await RpcAsync(payload, ct);
            if (doc == null) return Array.Empty<TorrentEntry>();

            var torrents = doc.RootElement
                .GetProperty("arguments")
                .GetProperty("torrents");

            var result = new List<TorrentEntry>();
            foreach (var t in torrents.EnumerateArray())
            {
                var entry = new TorrentEntry
                {
                    Id = t.GetProperty("id").GetInt32(),
                    Hash = t.GetProperty("hashString").GetString() ?? string.Empty,
                    Name = t.GetProperty("name").GetString() ?? string.Empty,
                    SavePath = t.GetProperty("downloadDir").GetString() ?? string.Empty,
                    Progress = t.GetProperty("percentDone").GetDouble(),
                    TotalSize = t.GetProperty("totalSize").GetInt64(),
                    State = MapState(t.GetProperty("status").GetInt32())
                };

                if (t.TryGetProperty("files", out var filesEl))
                    entry.Files = ParseFiles(filesEl, entry.Name);

                result.Add(entry);
            }
            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"Transmission GetTorrents: {ex.Message}");
            return Array.Empty<TorrentEntry>();
        }
    }

    private static List<TorrentFileEntry> ParseFiles(JsonElement filesEl, string torrentName)
    {
        var list = new List<TorrentFileEntry>();
        int idx = 0;
        foreach (var f in filesEl.EnumerateArray())
        {
            string relPath = f.GetProperty("name").GetString() ?? string.Empty;
            long length = f.GetProperty("length").GetInt64();
            long completed = f.GetProperty("bytesCompleted").GetInt64();

            list.Add(new TorrentFileEntry
            {
                Index = idx++,
                OriginalName = Path.GetFileName(relPath),
                RelativePath = relPath,
                Size = length,
                Progress = length > 0 ? completed / (double)length : 0
            });
        }
        return list;
    }

    public async Task<bool> RenameFileAsync(TorrentEntry torrent, TorrentFileEntry file, string newName, CancellationToken ct = default)
    {
        if (!IsConnected) return false;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await LogService.WriteAsync(LogLevel.Error, $"Transmission RenameFile : nom invalide '{newName}'");
            return false;
        }
        try
        {
            var payload = new
            {
                method = "torrent-rename-path",
                arguments = new
                {
                    ids = new[] { torrent.Id },
                    path = file.RelativePath,
                    name = newName
                }
            };

            using var doc = await RpcAsync(payload, ct);
            bool ok = doc?.RootElement.GetProperty("result").GetString() == "success";
            await LogService.WriteAsync(ok ? LogLevel.Info : LogLevel.Error,
                $"Transmission rename '{file.RelativePath}' → '{newName}': {(ok ? "OK" : "Échec")}");
            return ok;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"Transmission RenameFile: {ex.Message}");
            return false;
        }
    }

    private static TorrentState MapState(int status) => status switch
    {
        0 => TorrentState.Paused,
        1 or 2 => TorrentState.Checking,
        3 or 4 => TorrentState.Downloading,
        5 or 6 => TorrentState.Uploading,
        _ => TorrentState.Error
    };

    public void Dispose() => _http.Dispose();
}
