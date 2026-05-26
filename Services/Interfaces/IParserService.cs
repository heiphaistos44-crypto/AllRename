using AllRename.Models;

namespace AllRename.Services.Interfaces;

public record ParsedName(
    string CleanTitle,
    int? Year,
    int? Season,
    int? Episode,
    MediaType DetectedType
);

public interface IParserService
{
    ParsedName Parse(string fileName);
    string BuildTargetName(MediaInfo media, string extension);
}
