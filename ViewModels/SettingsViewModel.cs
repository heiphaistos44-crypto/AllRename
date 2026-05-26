using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AllRename.Services;
using AllRename.ViewModels.Base;

namespace AllRename.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly TmdbService _tmdb;
    private readonly PlexService _plex;
    private readonly SettingsService _settings;

    [ObservableProperty] private string _tmdbApiKey = string.Empty;
    [ObservableProperty] private string _plexServerUrl = string.Empty;
    [ObservableProperty] private string _plexToken = string.Empty;
    [ObservableProperty] private string _qbitUrl = "http://localhost:8080";
    [ObservableProperty] private string _qbitUsername = string.Empty;
    [ObservableProperty] private string _transmissionUrl = "http://localhost:9091";
    [ObservableProperty] private string _transmissionUsername = string.Empty;
    [ObservableProperty] private bool _plexConnected;
    [ObservableProperty] private bool _tmdbConfigured;

    public SettingsViewModel(TmdbService tmdb, PlexService plex, SettingsService settings)
    {
        _tmdb = tmdb;
        _plex = plex;
        _settings = settings;
    }

    public void LoadFromSettings()
    {
        TmdbApiKey = _settings.TmdbApiKey;
        PlexServerUrl = _settings.PlexServerUrl;
        PlexToken = _settings.PlexToken;
        QbitUrl = _settings.QbitUrl;
        QbitUsername = _settings.QbitUsername;
        TransmissionUrl = _settings.TransmissionUrl;
        TransmissionUsername = _settings.TransmissionUsername;

        if (!string.IsNullOrWhiteSpace(TmdbApiKey))
        {
            _tmdb.Configure(TmdbApiKey);
            TmdbConfigured = true;
        }
        if (!string.IsNullOrWhiteSpace(PlexServerUrl))
            _plex.Configure(PlexServerUrl, PlexToken);
    }

    [RelayCommand]
    private async Task SaveTmdbKeyAsync()
    {
        _tmdb.Configure(TmdbApiKey);
        TmdbConfigured = !string.IsNullOrWhiteSpace(TmdbApiKey);
        _settings.TmdbApiKey = TmdbApiKey;
        await _settings.SaveAsync();
        StatusMessage = TmdbConfigured ? "Clé TMDB enregistrée." : "Clé TMDB vide.";
    }

    [RelayCommand]
    private async Task TestPlexAsync()
    {
        _plex.Configure(PlexServerUrl, PlexToken);
        _settings.PlexServerUrl = PlexServerUrl;
        _settings.PlexToken = PlexToken;
        await _settings.SaveAsync();
        IsBusy = true;
        StatusMessage = "Test connexion Plex...";
        PlexConnected = await _plex.TestConnectionAsync();
        StatusMessage = PlexConnected ? "Plex connecté." : "Connexion Plex échouée.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task SaveTorrentSettingsAsync()
    {
        _settings.QbitUrl = QbitUrl;
        _settings.QbitUsername = QbitUsername;
        _settings.TransmissionUrl = TransmissionUrl;
        _settings.TransmissionUsername = TransmissionUsername;
        await _settings.SaveAsync();
        StatusMessage = "Paramètres torrents enregistrés.";
    }
}
