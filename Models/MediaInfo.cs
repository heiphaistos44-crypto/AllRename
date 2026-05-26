namespace AllRename.Models;

public enum MediaType { Movie, Series, Anime }

public class MediaInfo
{
    public int TmdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public int Year { get; set; }
    public MediaType Type { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public double MatchConfidence { get; set; }
    public string Source { get; set; } = string.Empty;
}
