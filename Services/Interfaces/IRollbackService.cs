using AllRename.Models;

namespace AllRename.Services.Interfaces;

public interface IRollbackService
{
    Task SaveBatchAsync(RollbackBatch batch);
    Task<RollbackBatch?> LoadLastBatchAsync();
    Task<bool> RollbackAsync(RollbackBatch batch, CancellationToken ct = default);
    Task PurgeBatchesAsync(int keepCount = 5);
}
