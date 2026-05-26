using System.Windows;
using Microsoft.Extensions.Caching.Memory;
using AllRename.Services;
using AllRename.ViewModels;

namespace AllRename;

public partial class App : Application
{
    public static TmdbService TmdbService { get; } = new(new MemoryCache(new MemoryCacheOptions()));
    public static PlexService PlexService { get; } = new();
    public static QBittorrentService QbitService { get; } = new();
    public static TransmissionService TransmissionService { get; } = new();

    private static readonly ParserService SharedParser = new();

    public static MainViewModel MainVm { get; } = new(
        new FileScanner(),
        new RenamerCore(SharedParser, TmdbService, PlexService),
        new RollbackService());

    public static SettingsViewModel SettingsVm { get; } = new(TmdbService, PlexService);

    public static TorrentViewModel TorrentVm { get; } = new(
        QbitService, TransmissionService, SharedParser, TmdbService);

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.PurgeOldLogs();
        base.OnExit(e);
    }
}
