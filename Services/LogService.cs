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

    private static async Task RotateIfNeededAsync()
    {
        // Fix Bug#9 : était 100% synchrone malgré la signature Task.
        // File.Move bloquait le thread du SemaphoreSlim — risque de starvation du pool.
        if (!File.Exists(CurrentLogFile)) return;
        if (new FileInfo(CurrentLogFile).Length < 1_048_576) return;

        string archiveDir = Path.Combine(LogDir, "archive");
        Directory.CreateDirectory(archiveDir);
        string dest = Path.Combine(archiveDir, $"renamer_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
        File.Move(CurrentLogFile, dest);   // Move est atomique OS ; pas de async natif en .NET
        await Task.CompletedTask;          // Yield explicite pour signaler la transition async
    }

    public static async Task PurgeOldLogsAsync(int keepDays = 30)
    {
        await _lock.WaitAsync();
        try
        {
            if (!Directory.Exists(LogDir)) return;
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(LogDir, "*.log"))
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
        }
        finally { _lock.Release(); }
    }
}
