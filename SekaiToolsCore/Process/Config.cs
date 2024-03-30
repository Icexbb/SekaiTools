namespace SekaiToolsCore.Process;

public class Config
{
    public string VideoFilePath { get; }
    public string ScriptFilePath { get; }
    public string TranslateFilePath { get; }
    public string OutputFilePath { get; }

    public struct TypewriterSetting(int fadeTime, int charTime)
    {
        public readonly int FadeTime = fadeTime;
        public readonly int CharTime = charTime;
    }

    public TypewriterSetting TyperSetting = new(50, 80);


    public Config(string videoFilePath, string scriptFilePath,
        string translateFilePath = "", string outputFilePath = "")
    {
        if (!Path.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found.", videoFilePath);
        if (!Path.Exists(scriptFilePath))
            throw new FileNotFoundException("Script file not found.", scriptFilePath);

        VideoFilePath = videoFilePath;
        ScriptFilePath = scriptFilePath;

        if (translateFilePath != "" && !Path.Exists(translateFilePath))
            throw new FileNotFoundException("Translation file not found.", translateFilePath);
        TranslateFilePath = translateFilePath;
        if (outputFilePath != "")
        {
            if (Path.GetExtension(outputFilePath) != ".ass")
                throw new FileNotFoundException("Output Path must be a .ass file ", outputFilePath);
            OutputFilePath = outputFilePath;
        }
        else
        {
            OutputFilePath = Path.Join(Path.GetDirectoryName(videoFilePath),
                "[STGenerated] " + Path.GetFileNameWithoutExtension(videoFilePath) + ".ass");
        }
    }

    public void SetSubtitleTyperSetting(int fadeTime, int charTime)
    {
        TyperSetting = new TypewriterSetting(fadeTime, charTime);
    }

    public override int GetHashCode()
    {
        var contents = $"{VideoFilePath}{ScriptFilePath}{TranslateFilePath}{OutputFilePath}";
        return contents.GetHashCode();
    }
}