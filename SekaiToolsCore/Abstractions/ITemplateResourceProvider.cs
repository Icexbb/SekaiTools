namespace SekaiToolsCore.Abstractions;

/// <summary>
///     为视频处理引擎提供模板和字体资源路径。
///     下载、校验和存储位置由宿主应用负责。
/// </summary>
public interface ITemplateResourceProvider
{
    string GetVideoProcessResourcePath(string fileName);
}
