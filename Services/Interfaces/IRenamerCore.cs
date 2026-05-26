using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface IRenamerCore
{
    Task<IReadOnlyList<FileEntry>> SimulateAsync(
        IEnumerable<string> filePaths,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default);

    Task<RollbackBatch> ExecuteAsync(
        IEnumerable<FileEntry> entries,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default);
}
