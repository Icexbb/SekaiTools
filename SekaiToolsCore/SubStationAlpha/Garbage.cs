namespace SekaiToolsCore.SubStationAlpha;

public class Garbage(string video = "", string audio = "")
{
    public override string ToString()
    {
        return $"[Aegisub Project Garbage]\nAudio File: {audio}\nVideo File: {video}";
    }
}