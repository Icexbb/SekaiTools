using System.Text;

namespace SekaiToolsBase.SubStationAlpha;

public class Subtitle(
    ScriptInfo scriptInfo,
    Garbage garbage,
    Styles styles,
    Events events)
{
    public Events Events => events;

    public override string ToString()
    {
        return $"{scriptInfo}\n\n{garbage}\n\n{styles}\n\n{events}\n";
    }

    public void Save(string path)
    {
        using var stream = File.Create(path);
        stream.Write(Encoding.UTF8.GetBytes(ToString()));
    }
}