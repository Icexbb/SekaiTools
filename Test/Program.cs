using SekaiToolsGUI;

namespace Test;

public static class Program
{
    public static void Main()
    {
        var path = @"C:\Users\icexb\SekaiTools\Data\source.json";
        ExplorerHelper.OpenFolderAndFocus(path);
    }
}