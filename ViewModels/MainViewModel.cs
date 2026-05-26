using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AllRename.Models;
using AllRename.Services;
using AllRename.Services.Interfaces;
using AllRename.ViewModels.Base;

namespace AllRename.ViewModels;

public sealed partial class MainViewModel : BaseViewModel
{
    private readonly IFileScanner _scanner;
    private readonly IRenamerCore _renamer;
    private readonly IRollbackService _rollback;

    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 100;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteRenameCommand))]
    private bool _simulationReady;

    public ObservableCollection<FileEntry> FileEntries { get; } = new();

    public MainViewModel(IFileScanner scanner, IRenamerCore renamer, IRollbackService rollback)
    {
        _scanner = scanner;
        _renamer = renamer;
        _rollback = rollback;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SimulateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            StatusMessage = "Chemin invalide ou dossier introuvable.";
            return;
        }

        IsBusy = true;
        SimulationReady = false;
        FileEntries.Clear();
        StatusMessage = "Scan en cours...";

        try
        {
            var paths = new List<string>();
            await foreach (var entry in _scanner.ScanAsync(SourcePath, ct: ct))
                paths.Add(entry.SourcePath);

            ProgressMax = Math.Max(paths.Count, 1);
            StatusMessage = $"{paths.Count} fichier(s) détecté(s). Simulation en cours...";

            var progress = new Progress<(int current, int total)>(p =>
            {
                ProgressValue = p.current;
                StatusMessage = $"Simulation {p.current}/{p.total}...";
            });

            var results = await _renamer.SimulateAsync(paths, progress, ct);

            foreach (var r in results)
                FileEntries.Add(r);

            SimulationReady = FileEntries.Count > 0;
            int matched = FileEntries.Count(e => e.Status == MatchStatus.Matched);
            int partial = FileEntries.Count(e => e.Status == MatchStatus.Partial);
            int notFound = FileEntries.Count(e => e.Status == MatchStatus.NotFound);
            StatusMessage = $"Simulation terminée — Matched: {matched} | Partiels: {partial} | Introuvables: {notFound}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Simulation annulée.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur simulation : {ex.Message}";
            await LogService.WriteAsync(LogLevel.Error, $"SimulateAsync: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(SimulationReady), IncludeCancelCommand = true)]
    private async Task ExecuteRenameAsync(CancellationToken ct)
    {
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = FileEntries.Count;
        StatusMessage = "Renommage en cours...";

        try
        {
            var progress = new Progress<(int current, int total)>(p =>
            {
                ProgressValue = p.current;
                StatusMessage = $"Renommage {p.current}/{p.total}...";
            });

            var batch = await _renamer.ExecuteAsync(FileEntries, progress, ct);
            await _rollback.SaveBatchAsync(batch);
            await _rollback.PurgeBatchesAsync();

            StatusMessage = $"Terminé — {batch.Entries.Count} fichier(s) renommé(s). Annulation disponible (Ctrl+Z).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur exécution : {ex.Message}";
            await LogService.WriteAsync(LogLevel.Error, $"ExecuteRenameAsync: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        var batch = await _rollback.LoadLastBatchAsync();
        if (batch == null)
        {
            StatusMessage = "Aucun rollback disponible.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Annulation en cours...";
        bool ok = await _rollback.RollbackAsync(batch);
        StatusMessage = ok
            ? $"Rollback effectué — {batch.Entries.Count} fichier(s) restauré(s)."
            : "Rollback partiel — voir les logs pour les erreurs.";
        IsBusy = false;
    }
}
