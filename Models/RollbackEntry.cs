namespace AllRename.Models;

public class RollbackBatch
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RollbackEntry> Entries { get; set; } = new();
}

public class RollbackEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
}
