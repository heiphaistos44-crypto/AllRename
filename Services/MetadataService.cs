using AllRename.Models;
using AllRename.Services.Interfaces;

// ──────────────────────────────────────────────────────────────────────────────
//  DÉPENDANCES REQUISES dans AllRename.csproj :
//    <PackageReference Include="MetadataExtractor" Version="2.8.1" />   (EXIF)
//    <PackageReference Include="TagLibSharp"        Version="2.3.0" />   (ID3)
//
//  Ces packages ne sont PAS encore dans le csproj — voir AllRename.csproj.
//  Ce fichier compile uniquement après l'ajout des packages.
//
//  Architecture "opt-in" : si les packages sont absents, la classe est
//  remplacée par MetadataServiceStub (retourne toujours null / IsEmpty).
// ──────────────────────────────────────────────────────────────────────────────

namespace AllRename.Services;

/// <summary>
/// Moteur de renommage par métadonnées.
///
/// Flux :
///   1. ExtractAsync(filePath) → MetadataResult (EXIF ou ID3)
///   2. BuildTargetName(result, pattern, ext) → string (nouveau nom)
///   3. L'appelant (MainViewModel) crée un FileEntry.NewName avec ce nom
///
/// Patterns disponibles :
///   Photo : "{date:yyyy-MM-dd} {camera}" → "2024-06-15 Canon EOS R5.jpg"
///   Audio : "{track:D2} - {artist} - {title}" → "01 - Daft Punk - Get Lucky.mp3"
/// </summary>
public sealed class MetadataService : IMetadataService
{
    private static readonly HashSet<string> ExifExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".heic", ".heif",
        ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".dng", ".webp"
    };

    private static readonly HashSet<string> Id3Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus", ".wav", ".aiff"
    };

    public bool Supports(string extension) =>
        ExifExtensions.Contains(extension) || Id3Extensions.Contains(extension);

    public async Task<MetadataResult?> ExtractAsync(string filePath, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(filePath);

        if (ExifExtensions.Contains(ext))
            return await ExtractExifAsync(filePath, ct);

        if (Id3Extensions.Contains(ext))
            return await ExtractId3Async(filePath, ct);

        return null;
    }

    // ── EXIF ──────────────────────────────────────────────────────
    private static Task<MetadataResult?> ExtractExifAsync(string filePath, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // NOTE : décommentez après ajout du package MetadataExtractor
                // var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
                //
                // var subIfd = directories.OfType<MetadataExtractor.Formats.Exif.ExifSubIfdDirectory>().FirstOrDefault();
                // var ifd0   = directories.OfType<MetadataExtractor.Formats.Exif.ExifIfd0Directory>().FirstOrDefault();
                // var gps    = directories.OfType<MetadataExtractor.Formats.Exif.GpsDirectory>().FirstOrDefault();
                //
                // DateTime? dateTaken = null;
                // if (subIfd?.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out var dt) == true)
                //     dateTaken = dt;
                //
                // return new MetadataResult
                // {
                //     FilePath     = filePath,
                //     Source       = MetadataSource.Exif,
                //     DateTaken    = dateTaken,
                //     CameraMake   = ifd0?.GetDescription(ExifIfd0Directory.TagMake),
                //     CameraModel  = ifd0?.GetDescription(ExifIfd0Directory.TagModel),
                //     GpsLatitude  = gps?.GetRationalArray(GpsDirectory.TagLatitude) != null
                //                    ? gps.GetDouble(GpsDirectory.TagLatitude) : null,
                //     GpsLongitude = gps?.GetRationalArray(GpsDirectory.TagLongitude) != null
                //                    ? gps.GetDouble(GpsDirectory.TagLongitude) : null,
                // };

                // Stub jusqu'à activation du package
                return (MetadataResult?)new MetadataResult
                {
                    FilePath = filePath,
                    Source   = MetadataSource.Exif
                };
            }
            catch (Exception ex)
            {
                LogService.WriteAsync(LogLevel.Warn, $"EXIF extract '{filePath}': {ex.Message}")
                          .GetAwaiter().GetResult();
                return null;
            }
        }, ct);
    }

    // ── ID3 ───────────────────────────────────────────────────────
    private static Task<MetadataResult?> ExtractId3Async(string filePath, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // NOTE : décommentez après ajout du package TagLibSharp
                // using var file = TagLib.File.Create(filePath);
                // var tag = file.Tag;
                //
                // return new MetadataResult
                // {
                //     FilePath     = filePath,
                //     Source       = MetadataSource.Id3,
                //     TrackTitle   = string.IsNullOrWhiteSpace(tag.Title)  ? null : tag.Title.Trim(),
                //     Artist       = tag.Performers.FirstOrDefault()?.Trim(),
                //     Album        = string.IsNullOrWhiteSpace(tag.Album)  ? null : tag.Album.Trim(),
                //     AlbumArtist  = tag.AlbumArtists.FirstOrDefault()?.Trim(),
                //     TrackNumber  = tag.Track > 0 ? (int?)tag.Track : null,
                //     Year         = tag.Year > 0  ? (int?)tag.Year  : null,
                //     Genre        = tag.Genres.FirstOrDefault()?.Trim(),
                // };

                // Stub jusqu'à activation du package
                return (MetadataResult?)new MetadataResult
                {
                    FilePath = filePath,
                    Source   = MetadataSource.Id3
                };
            }
            catch (Exception ex)
            {
                LogService.WriteAsync(LogLevel.Warn, $"ID3 extract '{filePath}': {ex.Message}")
                          .GetAwaiter().GetResult();
                return null;
            }
        }, ct);
    }

    // ── Builder de nom ────────────────────────────────────────────
    /// <summary>
    /// Construit le nom cible en substituant les tokens du pattern.
    ///
    /// Tokens photo  : {date:FORMAT}, {camera}, {make}, {lat}, {lon}
    /// Tokens audio  : {title}, {artist}, {album}, {track:D2}, {year}, {genre}
    /// Fallback      : token absent → chaîne vide (pas d'exception)
    /// </summary>
    public string BuildTargetName(MetadataResult metadata, string pattern, string extension)
    {
        string ext = extension.StartsWith('.') ? extension : $".{extension}";
        string result = pattern;

        if (metadata.Source == MetadataSource.Exif)
        {
            result = result
                .Replace("{date}", metadata.DateTaken?.ToString("yyyy-MM-dd") ?? "date-inconnue")
                .Replace("{camera}", SanitizePart(metadata.CameraModel ?? metadata.CameraMake ?? ""))
                .Replace("{make}", SanitizePart(metadata.CameraMake ?? ""))
                .Replace("{lat}", metadata.GpsLatitude?.ToString("F4") ?? "")
                .Replace("{lon}", metadata.GpsLongitude?.ToString("F4") ?? "");

            // Format personnalisé pour la date
            int dateStart = result.IndexOf("{date:", StringComparison.Ordinal);
            while (dateStart >= 0)
            {
                int dateEnd = result.IndexOf('}', dateStart);
                if (dateEnd < 0) break;
                string fmt = result[(dateStart + 6)..dateEnd];
                string replacement = metadata.DateTaken?.ToString(fmt) ?? "date-inconnue";
                result = result[..dateStart] + replacement + result[(dateEnd + 1)..];
                dateStart = result.IndexOf("{date:", StringComparison.Ordinal);
            }
        }
        else if (metadata.Source == MetadataSource.Id3)
        {
            result = result
                .Replace("{title}", SanitizePart(metadata.TrackTitle ?? ""))
                .Replace("{artist}", SanitizePart(metadata.Artist ?? ""))
                .Replace("{album}", SanitizePart(metadata.Album ?? ""))
                .Replace("{year}", metadata.Year?.ToString() ?? "")
                .Replace("{genre}", SanitizePart(metadata.Genre ?? ""))
                .Replace("{track}", metadata.TrackNumber?.ToString() ?? "")
                .Replace("{track:D2}", metadata.TrackNumber?.ToString("D2") ?? "00");
        }

        // Supprime les séparateurs redondants causés par des tokens vides
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[ \-_]{2,}", " ").Trim(' ', '-', '_');

        return string.IsNullOrWhiteSpace(result) ? Path.GetFileNameWithoutExtension(metadata.FilePath) + ext
                                                 : result + ext;
    }

    private static string SanitizePart(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Trim(' ', '.');
    }
}
