using System.IO;
using System.Text.Json;
using SekaiToolsMedia;

namespace SekaiToolsGUI.Service;

internal static class SuppressionPreferences
{
    private static string FilePath => Path.Combine(SekaiToolsInfrastructure.Resources.ResourceManager.DataBaseDir, "suppression-preferences.json");
    internal static X264EncodingSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var value = JsonSerializer.Deserialize<X264EncodingSettings>(File.ReadAllText(FilePath)) ?? new();
            value.Validate();
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    internal static void Save(X264EncodingSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, FilePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { SekaiToolsBase.Logger.Log($"保存压制偏好失败：{ex.Message}"); }
    }
}
