using System.Windows.Automation.Peers;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Control;

/// <summary>Expose the navigation container without replacing WPF-UI's item peers or templates.</summary>
public sealed class AccessibleNavigationView : NavigationView
{
    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationPeer(this);

    private sealed class NavigationPeer(AccessibleNavigationView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(NavigationView);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    }
}
