using System.Reflection;

namespace SekaiToolsGUI.ViewModel.Setting;

public partial class SettingPageModel
{
    private static (PropertyInfo Current, PropertyInfo Default) ResolveResetProperty(string name)
    {
        var current = typeof(SettingPageModel).GetProperty(name);
        var defaults = typeof(Model.Setting).GetProperty(name);
        if (current?.CanWrite != true || defaults == null || current.PropertyType != defaults.PropertyType)
            throw new ArgumentException($"不支持还原的设置项：{name}", nameof(name));
        return (current, defaults);
    }

    public bool ArePropertiesModified(IEnumerable<string> names) => names.Select(ResolveResetProperty)
        .Any(pair => !Equals(pair.Current.GetValue(this), pair.Default.GetValue(Model.Setting.Default)));

    public void ResetProperties(IEnumerable<string> names)
    {
        var properties = names.Select(ResolveResetProperty).ToArray();
        var wasSuspended = _suspendSaving;
        _suspendSaving = true;
        try
        {
            foreach (var pair in properties)
                pair.Current.SetValue(this, pair.Default.GetValue(Model.Setting.Default));
        }
        finally { _suspendSaving = wasSuspended; }
        if (!wasSuspended) SaveSetting();
    }
}
