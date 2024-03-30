using System.Text;

namespace SekaiToolsCore.SubStationAlpha;

public class Subtitle(
    ScriptInfo scriptInfo,
    Garbage garbage,
    Styles styles,
    Events events)
{
    public override string ToString()
    {
        return $"{scriptInfo}\n\n{garbage}\n\n{styles}\n\n{events}\n";
    }

    public void Save(string path)
    {
        File.OpenWrite(path).Write(Encoding.UTF8.GetBytes(ToString()));
    }
}