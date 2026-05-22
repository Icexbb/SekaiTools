namespace SekaiToolsAvalonia.ViewModel;

public class NavItem
{
    public string Content { get; set; } = "";
    public string Icon { get; set; } = "";
    public Type TargetPageType { get; set; } = null!;
    public bool CachePage { get; set; } = true;
}
