using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AllRename.Models;
using AllRename.Services;
using AllRename.Services.Interfaces;
using AllRename.ViewModels.Base;

namespace AllRename.ViewModels;

public sealed partial class TorrentViewModel : BaseViewModel
{
    private readonly QBittorrentService _qbit;
    private readonly TransmissionService _transmission;
    private readonly IParserService _parser;
    private readonly ITmdbService _tmdb;

    [ObservableProperty] private string _clientUrl = "http://localhost:8080";
    [ObservableProperty] private string _username = "admin";
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private TorrentClient _selectedClient = TorrentClient.QBittorrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFiles))]
    [NotifyCanExecuteChangedFor(nameof(AnalyseFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyRenameCommand))]
    private TorrentEntry? _selectedTorrent;

    public ObservableCollection<TorrentEntry> Torrents { get; } = new();

    public IReadOnlyList<TorrentFileEntry> SelectedFiles =>
        (IReadOnlyList<TorrentFileEntry>?)SelectedTorrent?.Files ?? Array.Empty<TorrentFileEntry>();

    private ITorrentService ActiveService =>
        SelectedClient == TorrentClient.QBittorrent ? _qbit : _transmission;

    public TorrentViewModel(
        QBittorrentService qbit,
        TransmissionService transmission,
        IParserService parser,
        ITmdbService tmdb)
    {
        _qbit = qbit;
        _transmission = transmission;
        _parser = parser;
        _tmdb = tmdb;
    }

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken ct)
    {
        IsBusy = true;
        StatusMessage = $"Connexion à {SelectedClient}...";

        bool ok = await ActiveService.ConnectAsync(ClientUrl, Username, Password, ct);
        StatusMessage = ok
            ? $"{SelectedClient} connecté. Chargement des torrents..."
            : $"Échec connexion {SelectedClient} — vérifier URL/identifiants.";

        if (ok)
            await LoadTorrentsAsync(ct);

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (!ActiveService.IsConnected) { StatusMessage = "Non connecté."; return; }
        await LoadTorrentsAsync(ct);
    }

    private async Task LoadTorrentsAsync(CancellationToken ct)
    {
        IsBusy = true;
        var list = await ActiveService.GetTorrentsAsync(ct);
        Torrents.Clear();
        foreach (var t in list) Torrents.Add(t);
        StatusMessage = $"{Torrents.Count} torrent(s) chargé(s).";
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTorrent))]
    private async Task AnalyseFilesAsync(CancellationToken ct)
    {
        if (SelectedTorrent == null || !_tmdb.IsConfigured)
        {
            StatusMessage = _tmdb.IsConfigured
                ? "Sélectionner un torrent."
                : "Clé TMDB non configurée — aller dans l'onglet Fichiers locaux → Paramètres.";
            return;
        }

        IsBusy = true;
        int matched = 0, found = 0;
        StatusMessage = "Analyse TMDB en cours...";

        foreach (var file in SelectedTorrent.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsMediaExtension(file.OriginalName))
            {
                file.Status = MatchStatus.NotFound;
                continue;
            }

            try
            {
                var parsed = _parser.Parse(file.OriginalName);
                if (string.IsNullOrWhiteSpace(parsed.CleanTitle))
                {
                    file.Status = MatchStatus.NotFound;
                    continue;
                }

                MediaInfo? info = parsed.DetectedType == MediaType.Movie
                    ? await _tmdb.SearchMovieAsync(parsed.CleanTitle, parsed.Year, ct)
                    : await _tmdb.SearchSeriesAsync(parsed.CleanTitle, parsed.Year, ct);

                if (info == null)
                {
                    file.Status = MatchStatus.NotFound;
                    continue;
                }

                if (parsed.Season.HasValue) info.Season = parsed.Season;
                if (parsed.Episode.HasValue) info.Episode = parsed.Episode;

                file.Media = info;
                file.SuggestedName = _parser.BuildTargetName(info, Path.GetExtension(file.OriginalName));
                file.Status = info.MatchConfidence >= 0.8 ? MatchStatus.Matched : MatchStatus.Partial;
                found++;
                if (file.Status == MatchStatus.Matched) matched++;
            }
            catch (Exception ex)
            {
                file.Status = MatchStatus.Error;
                await LogService.WriteAsync(LogLevel.Error, $"AnalyseFile '{file.OriginalName}': {ex.Message}");
            }
        }

        StatusMessage = $"Analyse terminée — {matched} match / {found} trouvés / {SelectedTorrent.Files.Count} fichiers.";
        IsBusy = false;

        // Notifier la UI que les fichiers ont changé
        OnPropertyChanged(nameof(SelectedFiles));
        ApplyRenameCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTorrentWithSuggestions))]
    private async Task ApplyRenameAsync(CancellationToken ct)
    {
        if (SelectedTorrent == null) return;

        var toRename = SelectedTorrent.Files
            .Where(f => f.IsIncluded && f.Status is MatchStatus.Matched or MatchStatus.Partial
                        && !string.IsNullOrWhiteSpace(f.SuggestedName))
            .ToList();

        if (toRename.Count == 0) { StatusMessage = "Aucun fichier à renommer."; return; }

        IsBusy = true;
        int ok = 0;
        for (int i = 0; i < toRename.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = toRename[i];
            StatusMessage = $"Renommage {i + 1}/{toRename.Count} : {file.OriginalName}...";

            bool success = await ActiveService.RenameFileAsync(SelectedTorrent, file, file.SuggestedName, ct);
            if (success)
            {
                file.OriginalName = file.SuggestedName;
                file.SuggestedName = string.Empty;
                file.Status = MatchStatus.Matched;
                ok++;
            }
            else
            {
                file.Status = MatchStatus.Error;
            }
        }

        StatusMessage = $"Renommage terminé — {ok}/{toRename.Count} fichier(s) renommé(s) dans {SelectedClient}.";
        IsBusy = false;
    }

    private bool HasSelectedTorrent => SelectedTorrent != null;
    private bool HasSelectedTorrentWithSuggestions =>
        SelectedTorrent?.Files.Any(f => !string.IsNullOrWhiteSpace(f.SuggestedName)) == true;

    private static bool IsMediaExtension(string name) =>
        new[] { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".flv", ".ts", ".mpeg", ".mpg" }
        .Contains(Path.GetExtension(name).ToLowerInvariant());
}
