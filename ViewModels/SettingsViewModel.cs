using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AllRename.Services;
using AllRename.ViewModels.Base;

namespace AllRename.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly TmdbService _tmdb;
    private readonly PlexService _plex;

    [ObservableProperty] private string _tmdbApiKey = string.Empty;
    [ObservableProperty] private string _plexServerUrl = string.Empty;
    [ObservableProperty] private string _plexToken = string.Empty;
    [ObservableProperty] private bool _plexConnected;
    [ObservableProperty] private bool _tmdbConfigured;

    public SettingsViewModel(TmdbService tmdb, PlexService plex)
    {
        _tmdb = tmdb;
        _plex = plex;
    }

    [RelayCommand]
    private void SaveTmdbKey()
    {
        _tmdb.Configure(TmdbApiKey);
        TmdbConfigured = !string.IsNullOrWhiteSpace(TmdbApiKey);
        StatusMessage = TmdbConfigured ? "Clé TMDB enregistrée." : "Clé TMDB vide.";
    }

    [RelayCommand]
    private async Task TestPlexAsync()
    {
        _plex.Configure(PlexServerUrl, PlexToken);
        IsBusy = true;
        StatusMessage = "Test connexion Plex...";
        PlexConnected = await _plex.TestConnectionAsync();
        StatusMessage = PlexConnected ? "Plex connecté." : "Connexion Plex échouée.";
        IsBusy = false;
    }
}
