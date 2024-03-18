using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SekaiToolsGUI.ViewModel;

public class ViewModelBase:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    private bool _isLoad;
    public bool IsLoad
    {
        get => _isLoad;
        set
        {
            _isLoad = value;
            OnPropertyChanged(nameof(IsLoad));
        }
    }
    
    private bool _update;
    public bool Update
    {
        get => _update;
        set
        {
            _update = value;
            OnPropertyChanged(nameof(Update));
        }
    }
}
