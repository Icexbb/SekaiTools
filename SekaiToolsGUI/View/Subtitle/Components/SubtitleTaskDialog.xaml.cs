using System.ComponentModel;
using System.IO;
using System.Windows;
using SekaiToolsBase.Story;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTaskDialog : ContentDialog
{
    private int _validationVersion;
    private bool _materialsValid;
    private bool _submitting;

    public SubtitleTaskDialog(ContentDialogHost host) : base(host)
    {
        ViewModel = new SubtitlePageModel();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += OnMaterialChanged;
        Loaded += async (_, _) => await ValidateFilesAsync();
        Unloaded += (_, _) => ++_validationVersion;
    }

    public SubtitlePageModel ViewModel { get; }

    private async void OnMaterialChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SubtitlePageModel.ScriptFilePath) or nameof(SubtitlePageModel.TranslateFilePath))
            await ValidateFilesAsync();
        else if (e.PropertyName == nameof(SubtitlePageModel.VideoFilePath))
            UpdateSubmitButton();
    }

    private void UpdateSubmitButton()
    {
        IsPrimaryButtonEnabled = !_submitting && _materialsValid && ViewModel.CanStart && File.Exists(ViewModel.VideoFilePath);
    }

    private async Task<bool> ValidateFilesAsync()
    {
        var version = ++_validationVersion;
        var script = ViewModel.ScriptFilePath;
        var translation = ViewModel.TranslateFilePath;
        _materialsValid = false;
        UpdateSubmitButton();
        MaterialValidation.Title = "正在检查文件";
        MaterialValidation.Message = "正在读取剧本与翻译，检查条目是否匹配……";
        MaterialValidation.Severity = InfoBarSeverity.Informational;
        var result = await Task.Run(() => SubtitleMaterialValidation.Check(script, translation));
        if (version != _validationVersion || script != ViewModel.ScriptFilePath || translation != ViewModel.TranslateFilePath)
            return false;
        _materialsValid = result.CanUse;
        MaterialValidation.Title = result.Title;
        MaterialValidation.Message = result.Details;
        MaterialValidation.Severity = result.CanUse ? InfoBarSeverity.Success : result.IsPending ? InfoBarSeverity.Informational : InfoBarSeverity.Error;
        UpdateSubmitButton();
        return result.CanUse;
    }

    protected override async void OnButtonClick(ContentDialogButton button)
    {
        if (button != ContentDialogButton.Primary)
        {
            ++_validationVersion;
            base.OnButtonClick(button);
            return;
        }
        if (_submitting) return;
        _submitting = true;
        UpdateSubmitButton();
        try
        {
            // Re-read on submission in case a selected file changed on disk.
            if (!await ValidateFilesAsync()) return;
            if (!File.Exists(ViewModel.VideoFilePath))
            {
                MaterialValidation.Title = "视频文件不可用";
                MaterialValidation.Message = "视频文件不存在或无法访问，请重新选择。";
                MaterialValidation.Severity = InfoBarSeverity.Error;
                return;
            }
            _submitting = false;
            UpdateSubmitButton();
            base.OnButtonClick(button);
        }
        finally
        {
            _submitting = false;
            UpdateSubmitButton();
        }
    }
}
