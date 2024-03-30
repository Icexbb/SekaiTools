namespace SekaiToolsCore.SubStationAlpha;

public class ScriptInfo(int playRexX, int playRexY, string title = "", string scriptType = "v4.00+")
{
    public override string ToString()
    {
        return
            $"[Script Info]\nTitle: {title}\nScriptType: {scriptType}\nPlayRexX: {playRexX}\nPlayRexY: {playRexY}";
    }
}