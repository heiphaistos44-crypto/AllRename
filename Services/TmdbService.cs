using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class TmdbService : ITmdbService, IDisposable
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string Language = "fr-FR";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private string _apiKey = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public TmdbService(IMemoryCache cache)
    {
        _cache = cache;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public void Configure(string apiKey) => _apiKey = apiKey.Trim();

    public async Task<MediaInfo?> SearchMovieAsync(string title, int? year = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        string key = $"movie:{title}:{year}";
        if (_cache.TryGetValue(key, out MediaInfo? cached)) return cached;

        try
        {
            string url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(title)}&language={Language}";
            if (year.HasValue) url += $"&year={year}";

            var result = await _http.GetFromJsonAsync<TmdbSearchResult>(url, JsonOpts, ct);
            var top = result?.Results?.FirstOrDefault();
            if (top == null) return null;

            var info = new MediaInfo
            {
                TmdbId = top.Id,
                Title = top.Title ?? top.OriginalTitle ?? title,
                OriginalTitle = top.OriginalTitle ?? string.Empty,
                Year = ParseYear(top.ReleaseDate),
                Type = MediaType.Movie,
                MatchConfidence = ComputeConfidence(title, top.Title ?? string.Empty),
                Source = "TMDB"
            };

            _cache.Set(key, info, TimeSpan.FromHours(24));
            return info;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"TMDB film '{title}': {ex.Message}");
            return null;
        }
    }

    public async Task<MediaInfo?> SearchSeriesAsync(string title, int? year = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        string key = $"tv:{title}:{year}";
        if (_cache.TryGetValue(key, out MediaInfo? cached)) return cached;

        try
        {
            string url = $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(title)}&language={Language}";
            if (year.HasValue) url += $"&first_air_date_year={year}";

            var result = await _http.GetFromJsonAsync<TmdbSearchResult>(url, JsonOpts, ct);
            var top = result?.Results?.FirstOrDefault();
            if (top == null) return null;

            var info = new MediaInfo
            {
                TmdbId = top.Id,
                Title = top.Name ?? top.OriginalName ?? title,
                OriginalTitle = top.OriginalName ?? string.Empty,
                Year = ParseYear(top.FirstAirDate),
                Type = MediaType.Series,
                MatchConfidence = ComputeConfidence(title, top.Name ?? string.Empty),
                Source = "TMDB"
            };

            _cache.Set(key, info, TimeSpan.FromHours(24));
            return info;
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"TMDB série '{title}': {ex.Message}");
            return null;
        }
    }

    private static int ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return 0;
        return DateTime.TryParse(date, out var d) ? d.Year : 0;
    }

    private static double ComputeConfidence(string query, string result)
    {
        if (string.IsNullOrWhiteSpace(result)) return 0;
        var q = query.ToLowerInvariant().Trim();
        var r = result.ToLowerInvariant().Trim();
        if (q == r) return 1.0;
        if (r.Contains(q) || q.Contains(r)) return 0.8;
        int commonWords = q.Split(' ').Count(w => w.Length > 2 && r.Contains(w));
        return Math.Min(commonWords / (double)Math.Max(q.Split(' ').Length, 1) * 0.7, 0.75);
    }

    public void Dispose() => _http.Dispose();

    private sealed record TmdbSearchResult(List<TmdbItem>? Results);
    private sealed record TmdbItem(
        int Id,
        string? Title,
        string? OriginalTitle,
        string? Name,
        string? OriginalName,
        string? ReleaseDate,
        string? FirstAirDate);
}
