using System.Net.Http;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class PlexService : IPlexService, IDisposable
{
    private readonly HttpClient _http;
    private string _serverUrl = string.Empty;
    private string _token = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_serverUrl) && !string.IsNullOrWhiteSpace(_token);

    public PlexService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public void Configure(string serverUrl, string token)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _token = token.Trim();
    }

    public async Task<MediaInfo?> LookupAsync(string title, MediaType type, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            // Plex API : /library/search retourne du XML
            string url = $"{_serverUrl}/library/search?query={Uri.EscapeDataString(title)}&X-Plex-Token={_token}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            // TODO: parser le XML Plex (MediaContainer > Video/Directory)
            // Retourne null en attendant la configuration Plex complète
            await LogService.WriteAsync(LogLevel.Info, $"Plex lookup '{title}' — réponse reçue, parsing XML à implémenter.");
            return null;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Warn, $"Plex lookup '{title}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return false;
        try
        {
            var response = await _http.GetAsync($"{_serverUrl}?X-Plex-Token={_token}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}
