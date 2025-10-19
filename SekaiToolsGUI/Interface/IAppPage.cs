using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.Interface;

public interface IAppPage<out T> : INavigableView<T>
{
    void OnNavigatedTo()
    {
    }
}