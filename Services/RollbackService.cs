using System.Text.Json;
using AllRename.Models;
using AllRename.Services.Interfaces;

namespace AllRename.Services;

public sealed class RollbackService : IRollbackService
{
    private static readonly string RollbackDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllRename", "rollback");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task SaveBatchAsync(RollbackBatch batch)
    {
        Directory.CreateDirectory(RollbackDir);
        string path = Path.Combine(RollbackDir, $"batch_{batch.BatchId}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(batch, JsonOpts));
        await LogService.WriteAsync(LogLevel.Info, $"Rollback sauvegardé : {path}");
    }

    public async Task<RollbackBatch?> LoadLastBatchAsync()
    {
        if (!Directory.Exists(RollbackDir)) return null;

        string? file = Directory.GetFiles(RollbackDir, "batch_*.json")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (file == null) return null;

        string json = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<RollbackBatch>(json);
    }

    public async Task<bool> RollbackAsync(RollbackBatch batch, CancellationToken ct = default)
    {
        int errors = 0;
        // Fix Bug#3 : LIFO obligatoire.
        // Exemple : A→B puis B→C (2 entrées).
        // Rollback forward tentera A←B alors que le fichier s'appelle déjà C → not found.
        // Reverse() : on défait d'abord C→B, puis B→A. Ordre correct.
        foreach (var entry in Enumerable.Reverse(batch.Entries))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(entry.NewPath))
                    File.Move(entry.NewPath, entry.OriginalPath, overwrite: false);
            }
            catch (Exception ex)
            {
                errors++;
                await LogService.WriteAsync(LogLevel.Error, $"Rollback '{entry.NewPath}': {ex.Message}");
            }
        }

        await LogService.WriteAsync(LogLevel.Info,
            $"Rollback terminé : {batch.Entries.Count - errors}/{batch.Entries.Count} restaurés.");
        return errors == 0;
    }

    public async Task PurgeBatchesAsync(int keepCount = 5)
    {
        if (!Directory.Exists(RollbackDir)) return;
        var toDelete = Directory.GetFiles(RollbackDir, "batch_*.json")
            .OrderByDescending(File.GetLastWriteTime)
            .Skip(keepCount);

        foreach (var file in toDelete)
        {
            File.Delete(file);
            await LogService.WriteAsync(LogLevel.Info, $"Rollback purgé : {file}");
        }
    }
}
