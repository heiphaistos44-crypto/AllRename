using CommunityToolkit.Mvvm.ComponentModel;

namespace AllRename.ViewModels.Base;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
}
