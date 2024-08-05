using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SekaiToolsGUI;

public class ViewModelBase : INotifyPropertyChanged
{
    private readonly Dictionary<string, object> _properties = new();
    public event PropertyChangedEventHandler? PropertyChanged;

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