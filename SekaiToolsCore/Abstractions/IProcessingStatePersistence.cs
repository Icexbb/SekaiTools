using SekaiToolsCore.Process;

namespace SekaiToolsCore.Abstractions;

/// <summary>
///     接收处理引擎产生的进度快照和历史记录。
/// </summary>
public interface IProcessingStatePersistence
{
    void SaveProgress(string saveKey, ProcessingState state);

    void AddHistory(ProcessingState state);
}
