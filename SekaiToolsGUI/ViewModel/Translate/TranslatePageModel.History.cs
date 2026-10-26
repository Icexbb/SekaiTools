using System.IO;
using SekaiToolsBase.GameScript;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class TranslatePageModel
{
    public sealed record EditValue(string Text, bool PreserveEmpty);
    private sealed record Change(int Index, EditValue Before, EditValue After);
    private readonly List<Change[]> _undo = [];
    private readonly Stack<Change[]> _redo = [];
    private EditValue[] _currentValues = [];
    private EditValue[] _savedValues = [];
    private bool _restoringHistory;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? ContentChanged;

    private IEnumerable<TranslateItemModel> EditableItems() => Events.SelectMany(line => line switch
    {
        LineDialogModel dialog => new[] { dialog.Character, dialog.Content },
        LineEffectModel effect => new[] { effect.Content },
        _ => Array.Empty<TranslateItemModel>()
    });

    public EditValue[] ReadValues() => EditableItems().Select(item => new EditValue(item.Translated, item.PreserveEmpty)).ToArray();

    private void ResetHistory()
    {
        _undo.Clear();
        _redo.Clear();
        _currentValues = ReadValues();
        NotifyHistory();
    }

    private void OnDialogContentChanged(object? sender, EventArgs e) => RecordEdit();

    private void RecordEdit()
    {
        if (_restoringHistory) return;
        var next = ReadValues();
        var changes = next.Select((value, index) => new Change(index, _currentValues[index], value))
            .Where(change => change.Before != change.After).ToArray();
        if (changes.Length == 0) return;
        _undo.Add(changes);
        if (_undo.Count > 500) _undo.RemoveAt(0);
        _redo.Clear();
        _currentValues = next;
        FinishEdit();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var changes = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        ApplyChanges(changes, false);
        _redo.Push(changes);
        FinishEdit();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var changes = _redo.Pop();
        ApplyChanges(changes, true);
        _undo.Add(changes);
        FinishEdit();
    }

    private void ApplyChanges(Change[] changes, bool forward)
    {
        var items = EditableItems().ToArray();
        _restoringHistory = true;
        try
        {
            foreach (var change in changes)
            {
                var value = forward ? change.After : change.Before;
                items[change.Index].PreserveEmpty = value.PreserveEmpty;
                items[change.Index].Translated = value.Text;
            }
        }
        finally { _restoringHistory = false; }
        _currentValues = ReadValues();
    }

    private void FinishEdit()
    {
        IsDirty = !_currentValues.SequenceEqual(_savedValues);
        NotifyHistory();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public GameScript? DraftScript => _loadedScript;

    public void RestoreDraft(GameScript script, string scriptPath, string translationPath, EditValue[] values, EditValue[] savedValues)
    {
        var expectedCount = new SekaiToolsBase.Story.Story(script, new SekaiToolsBase.Story.Translation.TranslationData(null))
            .Events.Sum(item => item is SekaiToolsBase.Story.StoryEvent.DialogStoryEvent ? 2 : 1);
        if (values.Length != expectedCount || savedValues.Length != expectedCount)
            throw new InvalidDataException("翻译草稿条目数量与剧本不匹配。");
        LoadScript(script, scriptPath);
        var before = ReadValues();
        ApplyChanges(values.Select((value, index) => new Change(index, before[index], value)).ToArray(), true);
        TranslationPath = translationPath;
        _savedValues = savedValues;
        ResetHistory();
        FinishEdit();
    }

    public EditValue[] SavedValues => _savedValues.ToArray();
}
