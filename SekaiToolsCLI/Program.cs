using System.Text;
using Emgu.CV;
using Newtonsoft.Json;
using SekaiToolsCore;

namespace SekaiToolsCLI;

public static class SekaiToolsCli
{
    private static bool CheckValidVideo(string filepath)
    {
        if (!Path.Exists(filepath)) return false;
        try
        {
            var vc = new VideoCapture(filepath);
            var result = vc.Read(new Mat());
            return result;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckValidJson(string filepath)
    {
        try
        {
            if (!Path.Exists(filepath)) return false;
            var jsonString = File.ReadAllText(filepath);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            if (data == null) return false;
            return data.ContainsKey("TalkData") &&
                   data.ContainsKey("Snippets") &&
                   data.ContainsKey("SpecialEffectData");
        }
        catch
        {
            return false;
        }
    }

    private static VideoProcessTaskConfig ParseArgToConfig(string jsonStr)
    {
        /* {
            "VideoFilePath":"",
            "ScriptFilePath":"",
            "TranslateFilePath":"",
            "OutputFilePath":"",
            "SubtitleTyperSetting":[]
            "Id":""
        } */
        Dictionary<string, object>? dict;
        try
        {
            dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);
        }
        catch (JsonReaderException)
        {
            var bytes = Convert.FromBase64String(jsonStr);
            jsonStr = Encoding.UTF8.GetString(bytes);
            dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);
        }

        if (dict == null)
            throw new Exception("Invalid Config");

        var videoFilePath = "";
        var scriptFilePath = "";
        var translateFilePath = "";
        var outputFilePath = "";
        var id = "";
        if (dict.TryGetValue("Id", out var valueId))
            id = valueId.ToString() ?? throw new Exception("Invalid Config");

        if (dict.TryGetValue("VideoFilePath", out var value0))
        {
            videoFilePath = value0.ToString() ?? "";
            if (!CheckValidVideo(videoFilePath))
                throw new Exception("File Not Supported");
        }

        if (dict.TryGetValue("ScriptFilePath", out var value1))
        {
            scriptFilePath = value1.ToString() ?? "";
            if (!CheckValidJson(scriptFilePath))
                throw new Exception("File Not Supported");
        }

        if (dict.TryGetValue("TranslateFilePath", out var value2))
        {
            translateFilePath = value2.ToString() ?? "";
            if (translateFilePath != "" && !Path.Exists(translateFilePath))
                throw new FileNotFoundException("Translate File Not Found", translateFilePath);
        }

        if (dict.TryGetValue("OutputFilePath", out var value3))
        {
            outputFilePath = value3.ToString() ?? "";
            if (outputFilePath == "")
                outputFilePath = Path.Combine(Path.GetDirectoryName(videoFilePath) ?? "",
                    Path.GetFileNameWithoutExtension(videoFilePath) + ".ass");
            if (!Path.GetExtension(outputFilePath).Equals(".ass", StringComparison.CurrentCultureIgnoreCase))
            {
                outputFilePath = outputFilePath
                    .Replace(Path.GetExtension(outputFilePath), ".ass");
            }
        }

        var config = new VideoProcessTaskConfig(id, videoFilePath, scriptFilePath, translateFilePath, outputFilePath);

        if (!dict.TryGetValue("SubtitleTyperSetting", out var value)) return config;
        if (value is not Newtonsoft.Json.Linq.JArray array) return config;
        var list = array.ToObject<List<int>>() ?? [];
        if (list.Count != 0) config.SetSubtitleTyperSetting(list[0], list.Count == 1 ? list[0] : list[1]);
        
        return config;
    }

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("No Config Found!");
        }

        var jsonStr = string.Join("", args);
        Console.WriteLine($"Parsing {jsonStr}");
        var config = ParseArgToConfig(jsonStr);

        var task = new VideoProcess(config);
        task.Process();
    }
}