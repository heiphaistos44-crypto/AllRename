using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AllRename.Models;

public enum MatchStatus { Pending, Matched, Partial, NotFound, Error }

public class FileEntry : INotifyPropertyChanged
{
    private bool _isIncluded = true;
    private MatchStatus _status = MatchStatus.Pending;
    private string _newName = string.Empty;
    private string? _errorMessage;

    public string SourcePath { get; init; } = string.Empty;
    public string SourceName => Path.GetFileName(SourcePath);
    public string Extension => Path.GetExtension(SourcePath);
    public string SourceDirectory => Path.GetDirectoryName(SourcePath) ?? string.Empty;

    public string NewName
    {
        get => _newName;
        set { _newName = value; OnPropertyChanged(); OnPropertyChanged(nameof(NewPath)); }
    }

    public string NewPath => Path.Combine(SourceDirectory, NewName);

    public MatchStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsIncluded
    {
        get => _isIncluded;
        set { _isIncluded = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public MediaInfo? Media { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
