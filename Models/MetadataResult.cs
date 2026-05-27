namespace AllRename.Models;

/// <summary>
/// Métadonnées extraites d'un fichier (EXIF pour images, ID3 pour audio).
/// Utilisées par le moteur de renommage par métadonnées (AXE 4 — Feature #3).
/// </summary>
public sealed class MetadataResult
{
    // ── Communes ──────────────────────────────────────────────────
    public string FilePath { get; init; } = string.Empty;
    public MetadataSource Source { get; init; }

    // ── EXIF (Photos / Vidéos) ────────────────────────────────────
    /// <summary>Date de prise de vue (EXIF DateTimeOriginal).</summary>
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public double? GpsLatitude { get; init; }
    public double? GpsLongitude { get; init; }

    // ── ID3 (MP3 / FLAC / AAC) ────────────────────────────────────
    public string? TrackTitle { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? AlbumArtist { get; init; }
    public int? TrackNumber { get; init; }
    public int? Year { get; init; }
    public string? Genre { get; init; }

    // ── Qualité ───────────────────────────────────────────────────
    public bool IsEmpty =>
        DateTaken is null
        && TrackTitle is null
        && Artist is null
        && Album is null;
}

public enum MetadataSource { None, Exif, Id3, Mixed }
