using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface IFileScanner
{
    IAsyncEnumerable<FileEntry> ScanAsync(string rootPath, IProgress<int>? progress = null, CancellationToken ct = default);
    bool IsMediaFile(string path);
}
