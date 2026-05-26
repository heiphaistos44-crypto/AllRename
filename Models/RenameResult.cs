namespace AllRename.Models;

public class RenameResult
{
    public string SourcePath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
