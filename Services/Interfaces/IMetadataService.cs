using AllRename.Models;

namespace AllRename.Services.Interfaces;

/// <summary>
/// Contrat du moteur de renommage par métadonnées.
/// Implémenté par <see cref="MetadataService"/>.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    /// Extrait les métadonnées d'un fichier (EXIF ou ID3 selon l'extension).
    /// Retourne null si le fichier ne supporte aucun format de métadonnées.
    /// </summary>
    Task<MetadataResult?> ExtractAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Génère un nom de fichier cible à partir des métadonnées.
    /// Ex: photo.jpg → "2024-06-15 Canon EOS R5.jpg"
    ///     track.mp3 → "01 - Daft Punk - Get Lucky.mp3"
    /// </summary>
    string BuildTargetName(MetadataResult metadata, string pattern, string extension);

    /// <summary>
    /// Retourne true si l'extension est supportée par ce service.
    /// </summary>
    bool Supports(string extension);
}
