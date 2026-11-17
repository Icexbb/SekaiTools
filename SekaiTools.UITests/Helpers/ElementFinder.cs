using FlaUI.Core.AutomationElements;

namespace SekaiTools.UITests.Helpers;

public sealed class ElementFinder
{
    public string? TargetId { get; private set; }

    public AutomationElement ById(AutomationElement root, string id, TimeSpan? timeout = null)
    {
        TargetId = id;
        return WaitHelper.Until(() => root.FindFirstDescendant(cf => cf.ByAutomationId(id)), $"AutomationId={id}", timeout);
    }
}
