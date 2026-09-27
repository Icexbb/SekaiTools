using SekaiDataFetch.List;

namespace SekaiToolsGUI.Interface;

public interface IRefreshable
{
    public Task Refresh(IProgress<ListRefreshProgress>? progress = null);
}
