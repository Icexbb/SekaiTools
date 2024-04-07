using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;

namespace SekaiToolsGUI.ViewModel;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly Dictionary<string, object> _properties = new();

    protected T GetProperty<T>(T defaultValue = default!, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (_properties.TryGetValue(propertyName, out var value))
            return (T)value;

        SetProperty(defaultValue, propertyName);
        return defaultValue;
    }

    protected void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (_properties.TryGetValue(propertyName, out var oldValue) &&
            EqualityComparer<T>.Default.Equals((T)oldValue, value))
            return;
        if (value != null) _properties[propertyName] = value;
        OnPropertyChanged(propertyName);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}