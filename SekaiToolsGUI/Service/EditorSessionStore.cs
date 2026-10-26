using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsBase.GameScript;
using SekaiToolsGUI.ViewModel.Translate;

namespace SekaiToolsGUI.Service;

/// <summary>Session data is separate from user preferences and exported translations.</summary>
public sealed class EditorSessionStore
{
    private readonly string _directory;
    public EditorSessionStore(string? directory = null) =>
        _directory = directory ?? Path.Combine(ResourceManager.DataBaseDir, "Data");

    public sealed record Draft(GameScript Script, int[] OriginalEffectIndices, string ScriptPath,
        string TranslationPath, TranslatePageModel.EditValue[] Values, TranslatePageModel.EditValue[] SavedValues);

    public string? ReadLastModule() => Read<string>("last-module.json");
    public void SaveLastModule(string module) => Write("last-module.json", module);
    public Type? ResolveLastModule(IEnumerable<Type> mainModules)
    {
        var modules = mainModules.ToArray();
        var saved = ReadLastModule();
        return modules.FirstOrDefault(module => module.Name == saved) ?? modules.FirstOrDefault();
    }

    public void RecordModule(Type page, IEnumerable<Type> mainModules)
    {
        if (mainModules.Contains(page)) SaveLastModule(page.Name);
    }
    public Draft? ReadDraft() => Read<Draft>("translation-draft.json");
    public bool SaveDraft(TranslatePageModel model)
    {
        if (!model.IsDirty || model.DraftScript is not { } script)
        {
            return DeleteDraft();
        }
        return Write("translation-draft.json", new Draft(script, script.OriginalEffectIndices, model.ScriptPath,
            model.TranslationPath, model.ReadValues(), model.SavedValues));
    }

    public bool DeleteDraft()
    {
        try { File.Delete(Path.Combine(_directory, "translation-draft.json")); return true; }
        catch (Exception e) { Log(e); return false; }
    }

    private T? Read<T>(string name)
    {
        try
        {
            var path = Path.Combine(_directory, name);
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default;
        }
        catch (Exception e) { Log(e); return default; }
    }

    private bool Write<T>(string name, T value)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, name);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception e) { Log(e); return false; }
    }

    private static void Log(Exception e) => Logger.Log($"保存或读取编辑会话失败: {e.Message}", LogLevel.Warning);
}
