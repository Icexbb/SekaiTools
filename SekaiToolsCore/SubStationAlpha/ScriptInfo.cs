namespace SekaiToolsCore.SubStationAlpha;

public class ScriptInfo(
    int playResX,
    int playResY,
    string title = "NoTitle",
    string yCbCrMatrix = "TV.709",
    string scriptType = "v4.00+")
{
    public override string ToString()
    {
        return
            $"[Script Info]\n" +
            $"Title: {title}\n" +
            $"ScriptType: {scriptType}\n" +
            $"YCbCr Matrix: {yCbCrMatrix}\n" +
            $"PlayResX: {playResX}\n" +
            $"PlayResY: {playResY}";
    }
}