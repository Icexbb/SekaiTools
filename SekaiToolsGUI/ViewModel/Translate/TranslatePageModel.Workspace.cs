namespace SekaiToolsGUI.ViewModel.Translate;

public partial class TranslatePageModel
{
    public string LastEditFeedback { get => GetProperty(string.Empty); private set => SetProperty(value); }
    public bool ShowReference
    {
        get => GetProperty(true);
        set { SetProperty(value); foreach (var line in Events) line.ShowReference = value; }
    }
    public string SearchText
    {
        get => GetProperty(string.Empty);
        set { SetProperty(value); RefreshVisibleEvents(); }
    }
    public int FilterIndex
    {
        get => GetProperty(0);
        set { SetProperty(value); RefreshVisibleEvents(); }
    }
    public LineModel[] VisibleEvents
    {
        get => GetProperty(Array.Empty<LineModel>());
        private set => SetProperty(value);
    }
    public int UntranslatedCount => Events.Count(line => line.IsUntranslated);
    public int ExplicitEmptyCount => Events.Count(line => line.IsExplicitEmpty);
    public int WarningCount => Events.Count(line => line.HasWarning);
    public string ProgressText => $"共 {Events.Length} 条 · 未译 {UntranslatedCount} · 留空 {ExplicitEmptyCount} · 有提示 {WarningCount}";
    public string VisibleCountText => $"显示 {VisibleEvents.Length} / {Events.Length} 条";

    public string GetExportSummary(bool useV2) => ProgressText + "\n" +
        (UntranslatedCount > 0 ? $"{UntranslatedCount} 条未译正文将沿用原文。\n" : string.Empty) +
        (ExplicitEmptyCount > 0 ? useV2
            ? $"{ExplicitEmptyCount} 条正文已标为留空，将导出为空白。\n"
            : "旧 TXT 无法可靠保留显式空白；重新载入时可能沿用原文或缺少条目，建议使用 TXT v2。\n" : string.Empty) +
        (WarningCount > 0 ? "校验提示供参考，可取消并使用“下一提示”逐项检查，也可继续保存未完成稿。" : "可以保存当前进度。");

    public void RefreshVisibleEvents()
    {
        var query = SearchText.Trim();
        var matches = Events.Where(line => FilterIndex switch
        {
            1 => line.IsUntranslated,
            2 => line.HasWarning,
            3 => line is LineDialogModel { IsFlashback: true },
            _ => true
        }).Where(line => query.Length == 0 || SearchableText(line).Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        // Retain the item containers and focus when membership did not change.
        if (!VisibleEvents.SequenceEqual(matches)) VisibleEvents = matches;
        OnPropertyChanged(nameof(VisibleCountText));
    }

    private static IEnumerable<string> SearchableText(LineModel line)
    {
        yield return line.Body.Original;
        yield return line.Body.Translated;
        yield return line.Body.Reference;
        yield return line.SequenceNumber.ToString();
        if (line is LineDialogModel dialog)
        {
            yield return dialog.Character.Original;
            yield return dialog.Character.Translated;
        }
    }

    public LineModel? FindNext(LineModel? current, bool warnings)
    {
        // Find the next item in script order before refreshing a filter that may remove current.
        var candidates = Events.Where(line => warnings ? line.HasWarning : line.IsUntranslated).ToArray();
        var next = candidates.FirstOrDefault(line => line.SequenceNumber > (current?.SequenceNumber ?? 0)) ?? candidates.FirstOrDefault();
        if (next == null) return null;
        if (!VisibleEvents.Contains(next)) { SearchText = string.Empty; FilterIndex = 0; }
        RefreshVisibleEvents();
        return next;
    }

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(UntranslatedCount));
        OnPropertyChanged(nameof(ExplicitEmptyCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void OnLineAction(object? sender, string action)
    {
        if (sender is not LineModel line || !Events.Contains(line)) return;
        _restoringHistory = true;
        try
        {
            switch (action)
            {
                case "blank": line.Body.PreserveEmpty = true; line.Body.Translated = string.Empty; break;
                case "untranslated": line.Body.PreserveEmpty = false; line.Body.Translated = string.Empty; break;
                case "original": line.Body.PreserveEmpty = false; line.Body.Translated = line.Body.Original; break;
                case "reference" when line.Body.ReferenceAvailable:
                    line.Body.PreserveEmpty = line.Body.ReferencePreserveEmpty;
                    line.Body.Translated = line.Body.Reference;
                    break;
            }
        }
        finally { _restoringHistory = false; }
        line.NotifyContentState();
        RecordEdit();
    }
}
