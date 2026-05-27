using System.Text.RegularExpressions;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class ParserService : IParserService
{
    // Tags techniques parasites
    private static readonly Regex TagPattern = new(
        @"\b(1080p|720p|480p|4K|2160p|UHD|HDR10?|HLG|DV|DoVi|" +
        @"MULTI|TRUEFRENCH|FRENCH|VOSTFR|VF|VO|VOBB|SUBFRENCH|" +
        @"x264|x265|H\.?264|H\.?265|HEVC|AVC|XviD|DivX|" +
        @"BluRay|BDRip|BDRemux|BRRip|WEBRip|WEB[-\.]DL|DVDRip|HDRip|HDTV|" +
        @"YIFY|YTS|PROPER|REPACK|EXTENDED|THEATRICAL|UNRATED|DIRECTORS\.?CUT|" +
        @"DD5\.1|DTS[-\.]?(?:HD|MA|X)?|TrueHD|ATMOS|AAC|AC3|MP3|FLAC|5\.1|7\.1|" +
        @"REMUX|COMPLETE|IMAX|3D|HFR|SDR|P2P|NF|AMZN|DSNP|HMAX|ATVP|STV|" +
        @"NordVPN|CCELL|FGT|YIFY|RARBG)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // S01E01 ou S01E01E02
    private static readonly Regex SeasonEpisodePattern = new(
        @"[Ss](\d{1,2})[Ee](\d{1,2})",
        RegexOptions.Compiled);

    // Format alternatif 1x01
    private static readonly Regex AltSeasonEpisodePattern = new(
        @"(?<!\d)(\d{1,2})x(\d{2})(?!\d)",
        RegexOptions.Compiled);

    // Année plausible (1950–2029)
    private static readonly Regex YearPattern = new(
        @"\(?(19[5-9]\d|20[012]\d)\)?",
        RegexOptions.Compiled);

    // Groupes entre crochets ou parenthèses contenant des infos release
    // Timeout 100ms : anti-ReDoS sur input adversarial (.*? imbriqué)
    private static readonly Regex GroupPattern = new(
        @"\[.*?\]|\((?:.*?(?:rip|hdtv|web|blu|dvd|xvid|divx|hevc|avc|x26).*?)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Séparateurs multiples → espace unique
    private static readonly Regex SeparatorPattern = new(
        @"[.\-_\[\]{}()\s]+",
        RegexOptions.Compiled);

    public ParsedName Parse(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);

        // 1. Détection format épisode → type Série
        int? season = null, episode = null;
        MediaType type = MediaType.Movie;

        var seMatch = SeasonEpisodePattern.Match(name);
        if (seMatch.Success)
        {
            season = int.Parse(seMatch.Groups[1].Value);
            episode = int.Parse(seMatch.Groups[2].Value);
            type = MediaType.Series;
            name = name[..seMatch.Index];
        }
        else
        {
            var altMatch = AltSeasonEpisodePattern.Match(name);
            if (altMatch.Success)
            {
                season = int.Parse(altMatch.Groups[1].Value);
                episode = int.Parse(altMatch.Groups[2].Value);
                type = MediaType.Series;
                name = name[..altMatch.Index];
            }
        }

        // 2. Extraction de l'année
        int? year = null;
        var yearMatch = YearPattern.Match(name);
        if (yearMatch.Success)
        {
            year = int.Parse(yearMatch.Value.Trim('(', ')'));
            name = name[..yearMatch.Index] + name[(yearMatch.Index + yearMatch.Length)..];
        }

        // 3. Suppression groupes de release (avec guard ReDoS)
        try { name = GroupPattern.Replace(name, " "); }
        catch (RegexMatchTimeoutException) { /* input anormal, on skip proprement */ }

        // 4. Suppression tags techniques
        name = TagPattern.Replace(name, " ");

        // 5. Normalisation séparateurs
        name = SeparatorPattern.Replace(name, " ").Trim();

        // 6. Capitalisation
        name = CapitalizeWords(name);

        return new ParsedName(name, year, season, episode, type);
    }

    public string BuildTargetName(MediaInfo media, string extension)
    {
        string ext = extension.StartsWith('.') ? extension : $".{extension}";
        string title = SanitizeForFileSystem(media.Title);

        return media.Type switch
        {
            MediaType.Movie =>
                $"{title} ({media.Year}){ext}",
            MediaType.Series or MediaType.Anime =>
                $"{title} ({media.Year}) - S{media.Season:D2}E{media.Episode:D2}{ext}",
            _ => throw new ArgumentOutOfRangeException(nameof(media.Type))
        };
    }

    private static string SanitizeForFileSystem(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim(' ', '.');
    }

    private static string CapitalizeWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            if (w.Length == 0) continue;

            // Fix Bug#1 : surrogate pair guard (emoji, CJK Extension, etc.)
            // Un emoji comme 🎬 = 2 chars UTF-16 (surrogate pair).
            // Appliquer char.ToUpper sur le high surrogate seul = corruption garantie.
            if (char.IsHighSurrogate(w[0]) && w.Length > 1 && char.IsLowSurrogate(w[1]))
            {
                // Préserver la paire intacte, lowercaser uniquement le reste
                words[i] = w[..2] + w[2..].ToLower();
            }
            else
            {
                words[i] = char.ToUpperInvariant(w[0]) + w[1..].ToLower();
            }
        }
        return string.Join(' ', words);
    }
}
