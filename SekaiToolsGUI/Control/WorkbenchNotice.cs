using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Control;

/// <summary>Preserve the Fluent notice while exposing its title and message to UIA readers.</summary>
public sealed class WorkbenchNotice : InfoBar
{
    protected override AutomationPeer OnCreateAutomationPeer() => new NoticePeer(this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != IsOpenProperty && e.Property != TitleProperty && e.Property != MessageProperty) return;
        if (UIElementAutomationPeer.FromElement(this) is not { } peer) return;
        peer.InvalidatePeer();
        if (IsOpen) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private sealed class NoticePeer(WorkbenchNotice owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(WorkbenchNotice);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.StatusBar;
        protected override string GetNameCore() => $"{owner.Title}：{owner.Message}";
        protected override bool IsControlElementCore() => owner.IsOpen;
        protected override AutomationLiveSetting GetLiveSettingCore() => AutomationLiveSetting.Polite;
    }
}
