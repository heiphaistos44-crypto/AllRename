using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AllRename.Models;

public enum TorrentState { Downloading, Uploading, Paused, Queued, Checking, Completed, Error }
public enum TorrentClient { QBittorrent, Transmission }

public class TorrentEntry
{
    public string Hash { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public TorrentState State { get; set; }
    public double Progress { get; set; }
    public long TotalSize { get; set; }
    public List<TorrentFileEntry> Files { get; set; } = new();

    public string ProgressLabel => $"{Progress * 100:F1}%";
    public string SizeLabel => FormatSize(TotalSize);

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        _                => $"{bytes / 1024.0:F0} KB"
    };
}

public class TorrentFileEntry : INotifyPropertyChanged
{
    private string _suggestedName = string.Empty;
    private MatchStatus _status = MatchStatus.Pending;
    private bool _isIncluded = true;

    public int Index { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public double Progress { get; set; }
    public MediaInfo? Media { get; set; }

    public string SuggestedName
    {
        get => _suggestedName;
        set { _suggestedName = value; OnPropertyChanged(); }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
