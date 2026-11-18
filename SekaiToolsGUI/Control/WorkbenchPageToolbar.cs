using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace SekaiToolsGUI.Control;

/// <summary>Wrap existing action controls in their original keyboard order.</summary>
public sealed class WorkbenchPageToolbar : WrapPanel
{
    public WorkbenchPageToolbar() => AutomationProperties.SetName(this, "页面操作");
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarPeer(this);

    private sealed class ToolbarPeer(WorkbenchPageToolbar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
        protected override string GetClassNameCore() => nameof(WorkbenchPageToolbar);
    }
}
