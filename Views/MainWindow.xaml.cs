using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AllRename.Models;

namespace AllRename.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Onglet Fichiers locaux ──────────────────────────────────────

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Sélectionner le dossier multimédia",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
            App.MainVm.SourcePath = dialog.FolderName;
    }

    private void SaveTmdbKey_Click(object sender, RoutedEventArgs e)
    {
        App.SettingsVm.TmdbApiKey = TmdbKeyBox.Text.Trim();
        App.SettingsVm.SaveTmdbKeyCommand.Execute(null);
    }

    private async void TestPlex_Click(object sender, RoutedEventArgs e)
    {
        App.SettingsVm.PlexServerUrl = PlexUrlBox.Text.Trim();
        App.SettingsVm.PlexToken = PlexTokenBox.Text.Trim();
        await App.SettingsVm.TestPlexCommand.ExecuteAsync(null);
        MessageBox.Show(App.SettingsVm.StatusMessage, "Plex", MessageBoxButton.OK,
            App.SettingsVm.PlexConnected ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Z && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
            App.MainVm.RollbackCommand.Execute(null);
    }

    // ── Onglet Torrents ─────────────────────────────────────────────

    private void ClientSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            App.TorrentVm.SelectedClient = cb.SelectedIndex == 0
                ? TorrentClient.QBittorrent
                : TorrentClient.Transmission;

            // URL par défaut selon le client
            App.TorrentVm.ClientUrl = cb.SelectedIndex == 0
                ? "http://localhost:8080"
                : "http://localhost:9091";
        }
    }

    private void TorrentPass_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
            App.TorrentVm.Password = pb.Password;
    }
}
