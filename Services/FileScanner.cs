using System.Runtime.CompilerServices;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class FileScanner : IFileScanner
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".ts",
        ".mpg", ".mpeg", ".m2ts", ".vob", ".divx", ".xvid", ".ogm", ".webm"
    };

    public async IAsyncEnumerable<FileEntry> ScanAsync(
        string rootPath,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int count = 0;
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.*", options))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsMediaFile(filePath)) continue;

            count++;
            progress?.Report(count);
            yield return new FileEntry { SourcePath = filePath };

            if (count % 50 == 0)
                await Task.Yield();
        }

        await LogService.WriteAsync(LogLevel.Info, $"Scan terminé : {count} fichiers dans '{rootPath}'.");
    }

    public bool IsMediaFile(string path) =>
        MediaExtensions.Contains(Path.GetExtension(path));
}
