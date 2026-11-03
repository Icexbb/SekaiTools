using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class TranslatePageModel
{
    private readonly Stack<Change[]> _redo = [];
    private readonly List<Change[]> _undo = [];
    private EditValue[] _currentValues = [];
    private TranslateItemModel? _editingItem;
    private long _lastTypingTime;
    private bool _mergeTyping;
    private bool _restoringHistory;
    private EditValue[] _savedValues = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public GameScript? DraftScript => _loadedScript;

    public EditValue[] SavedValues => _savedValues.ToArray();

    public void BeginTextEdit(TranslateItemModel item)
    {
        if (!ReferenceEquals(_editingItem, item)) BreakEditGroup();
        _editingItem = item;
    }

    public void EndTextEdit()
    {
        BreakEditGroup();
        _editingItem = null;
    }

    public void BreakEditGroup()
    {
        _mergeTyping = false;
    }

    public event EventHandler? ContentChanged;

    private IEnumerable<TranslateItemModel> EditableItems()
    {
        return Events.SelectMany(line => line switch
        {
            LineDialogModel dialog => new[] { dialog.Character, dialog.Content },
            LineEffectModel effect => new[] { effect.Content },
            _ => Array.Empty<TranslateItemModel>()
        });
    }

    public EditValue[] ReadValues()
    {
        return EditableItems().Select(item => new EditValue(item.Translated, item.PreserveEmpty)).ToArray();
    }

    private void ResetHistory()
    {
        EndTextEdit();
        _undo.Clear();
        _redo.Clear();
        _currentValues = ReadValues();
        NotifyHistory();
    }

    private void OnDialogContentChanged(object? sender, EventArgs e)
    {
        RecordEdit();
    }

    private void RecordEdit()
    {
        if (_restoringHistory) return;
        var next = ReadValues();
        var changes = next.Select((value, index) => new Change(index, _currentValues[index], value))
            .Where(change => change.Before != change.After).ToArray();
        if (changes.Length == 0) return;
        var now = Environment.TickCount64;
        var groupRemains = true;
        if (_editingItem != null && _mergeTyping && now - _lastTypingTime < 1500 && _undo.Count > 0)
        {
            var merged = _undo[^1].ToDictionary(change => change.Index);
            foreach (var change in changes)
                merged[change.Index] = merged.TryGetValue(change.Index, out var previous)
                    ? new Change(change.Index, previous.Before, change.After)
                    : change;
            _undo[^1] = merged.Values.Where(change => change.Before != change.After).ToArray();
            if (_undo[^1].Length == 0)
            {
                _undo.RemoveAt(_undo.Count - 1);
                groupRemains = false;
            }
        }
        else
        {
            _undo.Add(changes);
        }

        if (_undo.Count > 500) _undo.RemoveAt(0);
        _mergeTyping = groupRemains && _editingItem != null && _undo.Count > 0;
        _lastTypingTime = now;
        _redo.Clear();
        _currentValues = next;
        FinishEdit();
        LastEditFeedback = string.Empty;
    }

    public void Undo()
    {
        BreakEditGroup();
        if (!CanUndo) return;
        var changes = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        ApplyChanges(changes, false);
        _redo.Push(changes);
        FinishEdit();
        LastEditFeedback = "已撤销上一步修改。";
    }

    public void Redo()
    {
        BreakEditGroup();
        if (!CanRedo) return;
        var changes = _redo.Pop();
        ApplyChanges(changes, true);
        _undo.Add(changes);
        FinishEdit();
        LastEditFeedback = "已重做上一步修改。";
    }

    private void ApplyChanges(Change[] changes, bool forward)
    {
        var items = EditableItems().ToArray();
        _restoringHistory = true;
        foreach (var line in Events.OfType<LineDialogModel>()) line.RestoringText = true;
        try
        {
            foreach (var change in changes)
            {
                var value = forward ? change.After : change.Before;
                items[change.Index].PreserveEmpty = value.PreserveEmpty;
                items[change.Index].Translated = value.Text;
            }
        }
        finally
        {
            foreach (var line in Events.OfType<LineDialogModel>()) line.RestoringText = false;
            _restoringHistory = false;
        }

        _currentValues = ReadValues();
    }

    private void FinishEdit()
    {
        IsDirty = !_currentValues.SequenceEqual(_savedValues);
        MarkDraftPending();
        NotifyProgress();
        NotifyHistory();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void RestoreDraft(GameScript script, string scriptPath, string translationPath, EditValue[] values,
        EditValue[] savedValues, bool useV2 = true,
        bool autoFormat = true, int activeSequenceNumber = 0, bool syncMatchingContent = true,
        bool syncMatchingSpeakers = true)
    {
        var expectedCount = new Story(script, new TranslationData(null))
            .Events.Sum(item => item is DialogStoryEvent ? 2 : 1);
        if (values.Length != expectedCount || savedValues.Length != expectedCount)
            throw new InvalidDataException("翻译草稿条目数量与剧本不匹配。");
        AutoFormat = autoFormat;
        LoadScript(script, scriptPath);
        SyncMatchingContent = syncMatchingContent;
        SyncMatchingSpeakers = syncMatchingSpeakers;
        var before = ReadValues();
        ApplyChanges(values.Select((value, index) => new Change(index, before[index], value)).ToArray(), true);
        TranslationPath = translationPath;
        ExportUsesV2 = useV2;
        ActiveSequenceNumber = Math.Clamp(activeSequenceNumber, 0, Events.Length);
        _savedValues = savedValues;
        ResetHistory();
        FinishEdit();
    }

    public sealed record EditValue(string Text, bool PreserveEmpty);

    private sealed record Change(int Index, EditValue Before, EditValue After);
}