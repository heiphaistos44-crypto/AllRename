namespace AllRename.Services;

public enum LogLevel { Info, Warn, Error }

public static class LogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllRename", ".logs");

    private static string CurrentLogFile =>
        Path.Combine(LogDir, $"renamer_{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task WriteAsync(LogLevel level, string message)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(LogDir);
            await RotateIfNeededAsync();
            string line = $"[{DateTime.Now:o}] [{level.ToString().ToUpper()}] {message}";
            await File.AppendAllTextAsync(CurrentLogFile, line + Environment.NewLine);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static Task RotateIfNeededAsync()
    {
        if (!File.Exists(CurrentLogFile)) return Task.CompletedTask;
        if (new FileInfo(CurrentLogFile).Length < 1_048_576) return Task.CompletedTask;

        string archiveDir = Path.Combine(LogDir, "archive");
        Directory.CreateDirectory(archiveDir);
        string dest = Path.Combine(archiveDir, $"renamer_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
        File.Move(CurrentLogFile, dest);
        return Task.CompletedTask;
    }

    public static void PurgeOldLogs(int keepDays = 30)
    {
        if (!Directory.Exists(LogDir)) return;
        var cutoff = DateTime.Now.AddDays(-keepDays);
        foreach (var file in Directory.GetFiles(LogDir, "*.log"))
            if (File.GetLastWriteTime(file) < cutoff)
                File.Delete(file);
    }
}
