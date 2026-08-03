using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.View.Subtitle.Components;

public enum TimelineEventTrack
{
    Dialog,
    Banner,
    Marker
}

public sealed class TimelineEventSelection(
    BaseFrameSet frameSet,
    string eventNumber,
    string eventType,
    TimelineEventTrack track,
    string content,
    Brush accentBrush,
    Action timingChanged,
    Action? activated = null)
{
    public BaseFrameSet FrameSet { get; } = frameSet;
    public string EventNumber { get; } = eventNumber;
    public string EventType { get; } = eventType;
    public TimelineEventTrack Track { get; } = track;
    public string Content { get; } = content;
    public Brush AccentBrush { get; } = accentBrush;
    public Action TimingChanged { get; } = timingChanged;
    public Action? Activated { get; } = activated;
    public FrameRate FrameRate => FrameSet.Start().Fps;

    public int StartFrame => FrameSet.StartIndex();
    public int EndFrame => FrameSet.EndIndex();

    public (int StartFrame, int EndFrame) RecognizedFrameRange => FrameSet switch
    {
        DialogBaseFrameSet dialog => dialog.RecognizedFrameRange,
        BannerBaseFrameSet banner => banner.RecognizedFrameRange,
        MarkerBaseFrameSet marker => marker.RecognizedFrameRange,
        _ => (StartFrame, EndFrame)
    };

    public bool HasTimingEdits => FrameSet switch
    {
        DialogBaseFrameSet dialog => dialog.HasTimingEdits,
        BannerBaseFrameSet banner => banner.HasTimingEdits,
        MarkerBaseFrameSet marker => marker.HasTimingEdits,
        _ => false
    };

    public void SetFrameRange(int startFrame, int endFrame)
    {
        switch (FrameSet)
        {
            case DialogBaseFrameSet dialog:
                dialog.SetFrameRange(startFrame, endFrame);
                break;
            case BannerBaseFrameSet banner:
                banner.SetFrameRange(startFrame, endFrame);
                break;
            case MarkerBaseFrameSet marker:
                marker.SetFrameRange(startFrame, endFrame);
                break;
            default:
                throw new NotSupportedException($"不支持编辑 {FrameSet.GetType().Name} 的时间范围");
        }

        TimingChanged();
    }
}

public partial class TimelineEditor : UserControl
{
    private const double MaxPixelsPerSecond = 800;
    private const double HandleHitWidth = 11;
    private const double TrackHeaderWidth = 48;
    private readonly Stack<TimingEditCommand> _undoStack = new();
    private readonly List<TimelineEventSelection> _events = [];
    private readonly List<TimelineHitRegion> _eventHitRegions = [];
    private CancellationTokenSource? _waveformCancellation;
    private AudioWaveformEnvelope? _waveform;
    private StreamGeometry? _overviewWaveformGeometry;
    private AudioWaveformEnvelope? _overviewWaveformSource;
    private Size _overviewWaveformSize;
    private string _waveformStatus = "";
    private bool _overviewDragging;
    private TimelineEventSelection? _selection;
    private DragMode _dragMode;
    private int _dragAnchorFrame;
    private int _dragOriginalStart;
    private int _dragOriginalEnd;
    private double _pixelsPerSecond = 100;
    private double _viewStartMilliseconds;
    private int _videoDurationMilliseconds;
    private bool _updatingTimeBoxes;

    public TimelineEditor()
    {
        InitializeComponent();
        ClearSelection();
        UpdateReadOnlyState();
        UpdateUndoState();
        UpdateZoomText();
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(TimelineEditor),
        new FrameworkPropertyMetadata(false, OnIsReadOnlyChanged));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public void SetVideoDuration(int durationMilliseconds)
    {
        _videoDurationMilliseconds = Math.Max(0, durationMilliseconds);
        CoerceZoomToVideo();
        CoerceViewport();
        UpdateZoomText();
        RenderTimeline();
    }

    public async Task LoadAudioWaveformAsync(string videoPath)
    {
        _waveformCancellation?.Cancel();
        _waveformCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _waveformCancellation = cancellation;
        _waveform = null;
        _overviewWaveformGeometry = null;
        _waveformStatus = "正在生成音频波形…";
        RenderTimeline();

        try
        {
            var waveform = await AudioWaveformLoader.LoadAsync(videoPath, cancellation.Token);
            if (!ReferenceEquals(_waveformCancellation, cancellation))
                return;
            _waveform = waveform;
            _waveformStatus = "";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            if (!ReferenceEquals(_waveformCancellation, cancellation))
                return;
            _waveformStatus = "音频波形不可用";
        }
        finally
        {
            if (ReferenceEquals(_waveformCancellation, cancellation))
            {
                cancellation.Dispose();
                _waveformCancellation = null;
                RenderTimeline();
            }
        }
    }

    public void SelectEvent(TimelineEventSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        RegisterEvent(selection);
        _selection = selection;
        EventNumberText.Text = selection.EventNumber;
        EventTypeText.Text = selection.EventType;
        EventTypeBadge.Background = selection.AccentBrush;
        EventContentText.Text = selection.Content;
        EmptyHint.Visibility = Visibility.Collapsed;
        CenterOnSelection();
        UpdateTimingDisplay();
        RenderTimeline();
        Focus();
    }

    public void RegisterEvent(TimelineEventSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (_events.Any(item => ReferenceEquals(item.FrameSet, selection.FrameSet)))
            return;

        _events.Add(selection);
        RenderTimeline();
    }

    public void ClearSelection()
    {
        _selection = null;
        _waveformCancellation?.Cancel();
        _waveformCancellation?.Dispose();
        _waveformCancellation = null;
        _waveform = null;
        _overviewWaveformGeometry = null;
        _waveformStatus = "";
        _videoDurationMilliseconds = 0;
        _viewStartMilliseconds = 0;
        _events.Clear();
        _eventHitRegions.Clear();
        EventNumberText.Text = "未选择";
        EventTypeText.Text = "事件";
        EventTypeBadge.Background = Brushes.Gray;
        EventContentText.Text = "";
        EmptyHint.Visibility = Visibility.Visible;
        _undoStack.Clear();
        UpdateUndoState();
        UpdateTimingDisplay();
        RenderTimeline();
    }

    public bool Undo()
    {
        if (IsReadOnly || _undoStack.Count == 0)
            return false;

        var command = _undoStack.Pop();
        _selection = command.Selection;
        command.Selection.SetFrameRange(command.OldStartFrame, command.OldEndFrame);
        EventNumberText.Text = command.Selection.EventNumber;
        EventTypeText.Text = command.Selection.EventType;
        EventTypeBadge.Background = command.Selection.AccentBrush;
        EventContentText.Text = command.Selection.Content;
        EmptyHint.Visibility = Visibility.Collapsed;
        CenterOnSelection();
        UpdateTimingDisplay();
        UpdateUndoState();
        RenderTimeline();
        return true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Undo())
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TimelineEditor)d).UpdateReadOnlyState();
    }

    private void UpdateReadOnlyState()
    {
        if (!IsInitialized)
            return;

        ReadOnlyBadge.Visibility = IsReadOnly ? Visibility.Visible : Visibility.Collapsed;
        UpdateEditControlState();
        UpdateUndoState();
        RenderTimeline();
    }

    private void TimelineCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CoerceZoomToVideo();
        CoerceViewport();
        RenderTimeline();
    }

    private void OverviewCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderOverview();
    }

    private void OverviewCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _overviewDragging = true;
        OverviewCanvas.CaptureMouse();
        SetViewportFromOverview(e.GetPosition(OverviewCanvas).X);
        e.Handled = true;
    }

    private void OverviewCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_overviewDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        SetViewportFromOverview(e.GetPosition(OverviewCanvas).X);
        e.Handled = true;
    }

    private void OverviewCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_overviewDragging)
            return;

        _overviewDragging = false;
        OverviewCanvas.ReleaseMouseCapture();
        SetViewportFromOverview(e.GetPosition(OverviewCanvas).X);
        e.Handled = true;
    }

    private void SetViewportFromOverview(double x)
    {
        var plotWidth = GetOverviewPlotWidth();
        if (_videoDurationMilliseconds <= 0 || plotWidth <= 0)
            return;

        var ratio = Math.Clamp((x - TrackHeaderWidth) / plotWidth, 0, 1);
        var center = ratio * _videoDurationMilliseconds;
        _viewStartMilliseconds = center - GetVisibleMilliseconds() / 2;
        CoerceViewport();
        RenderTimeline();
    }

    private void TimelineCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        TimelineCanvas.Focus();
        var point = e.GetPosition(TimelineCanvas);
        if (_selection != null && !IsReadOnly)
        {
            var startX = TimeToX(GetStartMilliseconds(_selection));
            var endX = TimeToX(GetEndMilliseconds(_selection));
            if (Math.Abs(point.X - startX) <= HandleHitWidth)
                _dragMode = DragMode.Start;
            else if (Math.Abs(point.X - endX) <= HandleHitWidth)
                _dragMode = DragMode.End;
        }

        if (_dragMode == DragMode.None)
        {
            var hit = _eventHitRegions.LastOrDefault(item => item.Bounds.Contains(point));
            if (hit == null)
                return;

            if (!ReferenceEquals(hit.Selection, _selection))
            {
                SelectEvent(hit.Selection);
                hit.Selection.Activated?.Invoke();
                e.Handled = true;
                return;
            }

            if (IsReadOnly)
                return;
            _dragMode = DragMode.Range;
        }

        if (_selection == null || IsReadOnly)
            return;

        _dragOriginalStart = _selection.StartFrame;
        _dragOriginalEnd = _selection.EndFrame;
        _dragAnchorFrame = PointToFrame(point.X);
        TimelineCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void TimelineCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selection == null || _dragMode == DragMode.None || IsReadOnly)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CompleteDrag();
            return;
        }

        var frame = PointToFrame(e.GetPosition(TimelineCanvas).X);
        var start = _selection.StartFrame;
        var end = _selection.EndFrame;
        switch (_dragMode)
        {
            case DragMode.Start:
                start = Math.Clamp(frame, 0, end);
                break;
            case DragMode.End:
                end = Math.Max(start, frame);
                break;
            case DragMode.Range:
            {
                var delta = frame - _dragAnchorFrame;
                if (_dragOriginalStart + delta < 0)
                    delta = -_dragOriginalStart;
                start = _dragOriginalStart + delta;
                end = _dragOriginalEnd + delta;
                break;
            }
        }

        ApplyRangeCore(start, end);
        e.Handled = true;
    }

    private void TimelineCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CompleteDrag();
    }

    private void CompleteDrag()
    {
        if (_dragMode == DragMode.None)
            return;

        TimelineCanvas.ReleaseMouseCapture();
        var selection = _selection;
        _dragMode = DragMode.None;
        if (selection != null &&
            (_dragOriginalStart != selection.StartFrame || _dragOriginalEnd != selection.EndFrame))
        {
            _undoStack.Push(new TimingEditCommand(
                selection,
                _dragOriginalStart,
                _dragOriginalEnd,
                selection.StartFrame,
                selection.EndFrame));
            UpdateUndoState();
        }
    }

    private void TimelineCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomAt(e.GetPosition(TimelineCanvas).X, e.Delta > 0 ? 1.25 : 0.8);
        }
        else
        {
            var visibleMilliseconds = GetVisibleMilliseconds();
            _viewStartMilliseconds -= Math.Sign(e.Delta) * visibleMilliseconds * 0.15;
            CoerceViewport();
            RenderTimeline();
        }

        e.Handled = true;
    }

    private void StartMinusButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeRange(-1, 0);
    }

    private void StartPlusButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeRange(1, 0);
    }

    private void EndMinusButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeRange(0, -1);
    }

    private void EndPlusButton_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeRange(0, 1);
    }

    private void NudgeRange(int startDelta, int endDelta)
    {
        if (_selection == null || IsReadOnly)
            return;

        var start = Math.Max(0, _selection.StartFrame + startDelta);
        var end = Math.Max(start, _selection.EndFrame + endDelta);
        ApplyRangeWithUndo(start, end);
    }

    private void TimeBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CommitTimeBox((TextBox)sender);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void TimeBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitTimeBox((TextBox)sender);
    }

    private void CommitTimeBox(TextBox source)
    {
        if (_updatingTimeBoxes || _selection == null || IsReadOnly)
            return;
        if (!TryParseTimestamp(source.Text, out var milliseconds))
        {
            UpdateTimingDisplay();
            return;
        }

        var start = _selection.StartFrame;
        var end = _selection.EndFrame;
        if (ReferenceEquals(source, StartTimeBox))
            start = Math.Clamp(_selection.FrameRate.FrameAtTime(milliseconds, FrameType.Start), 0, end);
        else
            end = Math.Max(start, _selection.FrameRate.FrameAtTime(milliseconds, FrameType.End));

        ApplyRangeWithUndo(start, end);
    }

    private void ApplyRangeWithUndo(int start, int end)
    {
        if (_selection == null || IsReadOnly)
            return;

        var oldStart = _selection.StartFrame;
        var oldEnd = _selection.EndFrame;
        if (oldStart == start && oldEnd == end)
        {
            UpdateTimingDisplay();
            return;
        }

        ApplyRangeCore(start, end);
        _undoStack.Push(new TimingEditCommand(_selection, oldStart, oldEnd, start, end));
        UpdateUndoState();
    }

    private void ApplyRangeCore(int start, int end)
    {
        if (_selection == null)
            return;

        _selection.SetFrameRange(start, end);
        UpdateTimingDisplay();
        RenderTimeline();
    }

    private void UndoButton_OnClick(object sender, RoutedEventArgs e)
    {
        Undo();
    }

    private void RestoreTimingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selection == null || IsReadOnly || !_selection.HasTimingEdits)
            return;

        var recognized = _selection.RecognizedFrameRange;
        ApplyRangeWithUndo(recognized.StartFrame, recognized.EndFrame);
        CenterOnSelection();
        RenderTimeline();
    }

    private void ZoomOutButton_OnClick(object sender, RoutedEventArgs e)
    {
        ZoomAt(TimelineCanvas.ActualWidth / 2, 0.8);
    }

    private void ZoomInButton_OnClick(object sender, RoutedEventArgs e)
    {
        ZoomAt(TimelineCanvas.ActualWidth / 2, 1.25);
    }

    private void CenterButton_OnClick(object sender, RoutedEventArgs e)
    {
        CenterOnSelection();
        RenderTimeline();
    }

    private void ZoomAt(double x, double factor)
    {
        var plotWidth = GetPlotWidth();
        if (plotWidth <= 0)
            return;

        var anchorPlotX = Math.Clamp(x - TrackHeaderWidth, 0, plotWidth);
        var anchorTime = XToTime(x);
        _pixelsPerSecond = Math.Clamp(
            _pixelsPerSecond * factor,
            GetMinimumPixelsPerSecond(),
            GetMaximumPixelsPerSecond());
        _viewStartMilliseconds = anchorTime - anchorPlotX * 1000 / _pixelsPerSecond;
        CoerceViewport();
        UpdateZoomText();
        RenderTimeline();
    }

    private void CenterOnSelection()
    {
        if (_selection == null || GetPlotWidth() <= 0)
            return;

        var center = (GetStartMilliseconds(_selection) + GetEndMilliseconds(_selection)) / 2;
        _viewStartMilliseconds = center - GetVisibleMilliseconds() / 2;
        CoerceViewport();
    }

    private void CoerceZoomToVideo()
    {
        _pixelsPerSecond = Math.Clamp(
            _pixelsPerSecond,
            GetMinimumPixelsPerSecond(),
            GetMaximumPixelsPerSecond());
    }

    private void CoerceViewport()
    {
        if (_videoDurationMilliseconds <= 0)
        {
            _viewStartMilliseconds = Math.Max(0, _viewStartMilliseconds);
            return;
        }

        var maximumStart = Math.Max(0, _videoDurationMilliseconds - GetVisibleMilliseconds());
        _viewStartMilliseconds = Math.Clamp(_viewStartMilliseconds, 0, maximumStart);
    }

    private double GetMinimumPixelsPerSecond()
    {
        var plotWidth = GetPlotWidth();
        if (_videoDurationMilliseconds <= 0 || plotWidth <= 0)
            return 20;

        return plotWidth * 1000 / _videoDurationMilliseconds;
    }

    private double GetMaximumPixelsPerSecond()
    {
        return Math.Max(MaxPixelsPerSecond, GetMinimumPixelsPerSecond());
    }

    private void UpdateTimingDisplay()
    {
        _updatingTimeBoxes = true;
        try
        {
            var hasSelection = _selection != null;
            StartTimeBox.IsEnabled = hasSelection;
            EndTimeBox.IsEnabled = hasSelection;
            UpdateEditControlState();
            if (!hasSelection)
            {
                StartTimeBox.Text = "--:--:--.--";
                EndTimeBox.Text = "--:--:--.--";
                DurationText.Text = "--";
                return;
            }

            var start = GetStartMilliseconds(_selection!);
            var end = GetEndMilliseconds(_selection!);
            StartTimeBox.Text = FormatTimestamp(start);
            EndTimeBox.Text = FormatTimestamp(end);
            DurationText.Text = $"{Math.Max(0, end - start) / 1000d:0.00}s";
        }
        finally
        {
            _updatingTimeBoxes = false;
        }
    }

    private void UpdateEditControlState()
    {
        var canEdit = !IsReadOnly && _selection != null;
        StartTimeBox.IsReadOnly = !canEdit;
        EndTimeBox.IsReadOnly = !canEdit;
        StartMinusButton.IsEnabled = canEdit;
        StartPlusButton.IsEnabled = canEdit;
        EndMinusButton.IsEnabled = canEdit;
        EndPlusButton.IsEnabled = canEdit;
        RestoreTimingButton.IsEnabled = canEdit && _selection!.HasTimingEdits;
    }


    private void UpdateUndoState()
    {
        if (!IsInitialized)
            return;
        UndoButton.IsEnabled = !IsReadOnly && _undoStack.Count > 0;
        UndoButton.ToolTip = _undoStack.Count == 0
            ? "没有可撤回的时间修改"
            : $"撤回时间修改 (Ctrl+Z) · {_undoStack.Count}";
    }

    private void UpdateZoomText()
    {
        if (ZoomText != null)
            ZoomText.Text = $"{_pixelsPerSecond:0.##}px/s";
    }

    private void RenderTimeline()
    {
        if (TimelineCanvas == null)
            return;

        TimelineCanvas.Children.Clear();
        _eventHitRegions.Clear();
        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var gridBrush = TryFindResource("ControlStrokeColorDefaultBrush") as Brush ?? Brushes.DimGray;
        var textBrush = TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;
        var primaryBrush = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var majorInterval = GetMajorTickInterval();
        var minorInterval = majorInterval / 5;
        var viewEnd = _viewStartMilliseconds + GetVisibleMilliseconds();
        if (_videoDurationMilliseconds > 0)
            viewEnd = Math.Min(viewEnd, _videoDurationMilliseconds);
        var firstTick = Math.Floor(_viewStartMilliseconds / minorInterval) * minorInterval;

        for (var time = firstTick; time <= viewEnd + minorInterval; time += minorInterval)
        {
            if (time < 0)
                continue;
            var x = TimeToX(time);
            var isMajor = Math.Abs(time / majorInterval - Math.Round(time / majorInterval)) < 0.001;
            TimelineCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = isMajor ? 20 : 31,
                Y2 = height,
                Stroke = gridBrush,
                Opacity = isMajor ? 0.62 : 0.24,
                StrokeThickness = 1
            });

            if (!isMajor)
                continue;
            var label = new TextBlock
            {
                Text = FormatRulerTimestamp((int)Math.Round(time)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = textBrush
            };
            Canvas.SetLeft(label, x + 3);
            Canvas.SetTop(label, 2);
            TimelineCanvas.Children.Add(label);
        }

        const double waveformTop = 27;
        const double waveformHeight = 46;
        var audioLabel = new TextBlock
        {
            Text = "音频",
            FontSize = 10,
            Foreground = textBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(audioLabel, 6);
        Canvas.SetTop(audioLabel, waveformTop + 15);
        TimelineCanvas.Children.Add(audioLabel);
        TimelineCanvas.Children.Add(new Line
        {
            X1 = TrackHeaderWidth,
            X2 = width,
            Y1 = waveformTop + waveformHeight / 2,
            Y2 = waveformTop + waveformHeight / 2,
            Stroke = gridBrush,
            Opacity = 0.32,
            StrokeThickness = 1,
            IsHitTestVisible = false
        });
        DrawWaveform(
            TimelineCanvas,
            _viewStartMilliseconds,
            viewEnd,
            TrackHeaderWidth,
            width,
            waveformTop,
            waveformHeight,
            primaryBrush,
            0.28);
        if (_selection != null)
        {
            var highlightStart = Math.Max(_viewStartMilliseconds, GetStartMilliseconds(_selection));
            var highlightEnd = Math.Min(viewEnd, GetEndMilliseconds(_selection));
            if (highlightEnd > highlightStart)
            {
                DrawWaveform(
                    TimelineCanvas,
                    highlightStart,
                    highlightEnd,
                    TimeToX(highlightStart),
                    TimeToX(highlightEnd),
                    waveformTop,
                    waveformHeight,
                    primaryBrush,
                    0.92);
            }
        }
        if (_waveform == null && !string.IsNullOrWhiteSpace(_waveformStatus))
        {
            var status = new TextBlock
            {
                Text = _waveformStatus,
                FontSize = 10,
                Foreground = textBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(status, TrackHeaderWidth + 8);
            Canvas.SetTop(status, waveformTop + 15);
            TimelineCanvas.Children.Add(status);
        }

        const double trackTop = 78;
        const double laneHeight = 15;
        const double laneGap = 2;
        TimelineCanvas.Children.Add(new Line
        {
            X1 = TrackHeaderWidth,
            X2 = TrackHeaderWidth,
            Y1 = 20,
            Y2 = height,
            Stroke = gridBrush,
            Opacity = 0.62,
            StrokeThickness = 1
        });

        (TimelineEventTrack Track, string Label)[] tracks =
        [
            (TimelineEventTrack.Dialog, "对话"),
            (TimelineEventTrack.Banner, "横幅"),
            (TimelineEventTrack.Marker, "标记")
        ];
        foreach (var (track, trackLabel) in tracks)
        {
            var top = trackTop + (int)track * (laneHeight + laneGap);
            var label = new TextBlock
            {
                Text = trackLabel,
                FontSize = 10,
                Foreground = textBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, 6);
            Canvas.SetTop(label, top);
            TimelineCanvas.Children.Add(label);
            TimelineCanvas.Children.Add(new Line
            {
                X1 = TrackHeaderWidth,
                X2 = width,
                Y1 = top + laneHeight + 1,
                Y2 = top + laneHeight + 1,
                Stroke = gridBrush,
                Opacity = 0.28,
                StrokeThickness = 1
            });
        }

        foreach (var item in _events.OrderBy(item => ReferenceEquals(item, _selection) ? 1 : 0))
        {
            var startX = TimeToX(GetStartMilliseconds(item));
            var endX = TimeToX(GetEndMilliseconds(item));
            if (endX <= TrackHeaderWidth || startX >= width)
                continue;

            var isSelected = ReferenceEquals(item, _selection);
            var displayStartX = Math.Max(TrackHeaderWidth, startX);
            var displayEndX = Math.Min(width, endX);
            var blockWidth = Math.Max(3, displayEndX - displayStartX);
            var top = trackTop + (int)item.Track * (laneHeight + laneGap);
            var fill = item.AccentBrush is SolidColorBrush solid
                ? new SolidColorBrush(solid.Color) { Opacity = isSelected ? 0.42 : 0.22 }
                : new SolidColorBrush(Color.FromArgb(isSelected ? (byte)108 : (byte)56, 128, 128, 128));
            var range = new Rectangle
            {
                Width = blockWidth,
                Height = laneHeight,
                RadiusX = 2,
                RadiusY = 2,
                Fill = fill,
                Stroke = item.AccentBrush,
                StrokeThickness = isSelected ? 2 : 1,
                ToolTip = $"{item.EventNumber} {item.EventType} · {item.Content}"
            };
            Canvas.SetLeft(range, displayStartX);
            Canvas.SetTop(range, top);
            TimelineCanvas.Children.Add(range);
            _eventHitRegions.Add(new TimelineHitRegion(item, new Rect(displayStartX, top, blockWidth, laneHeight)));

            if (blockWidth >= 24)
            {
                var number = new TextBlock
                {
                    Text = item.EventNumber,
                    FontSize = 9,
                    Foreground = primaryBrush,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(number, displayStartX + 3);
                Canvas.SetTop(number, top - 1);
                TimelineCanvas.Children.Add(number);
            }
        }

        if (_selection != null)
        {
            var selectedTop = trackTop + (int)_selection.Track * (laneHeight + laneGap);
            DrawHandle(TimeToX(GetStartMilliseconds(_selection)), Brushes.IndianRed, true, selectedTop);
            DrawHandle(TimeToX(GetEndMilliseconds(_selection)), primaryBrush, false, selectedTop);
        }

        void DrawHandle(double x, Brush brush, bool pointsRight, double top)
        {
            if (x < TrackHeaderWidth || x > width)
                return;

            var flagTop = waveformTop;
            var flagBottom = waveformTop + 8;
            TimelineCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = flagTop,
                Y2 = top + laneHeight,
                Stroke = brush,
                StrokeThickness = 3
            });
            var points = pointsRight
                ? new PointCollection([new Point(x, flagTop), new Point(x + 8, flagTop), new Point(x, flagBottom)])
                : new PointCollection([new Point(x, flagTop), new Point(x - 8, flagTop), new Point(x, flagBottom)]);
            TimelineCanvas.Children.Add(new Polygon { Points = points, Fill = brush });
        }

        RenderOverview();
    }

    private void RenderOverview()
    {
        if (OverviewCanvas == null)
            return;

        OverviewCanvas.Children.Clear();
        var width = OverviewCanvas.ActualWidth;
        var height = OverviewCanvas.ActualHeight;
        var plotWidth = GetOverviewPlotWidth();
        if (width <= 0 || height <= 0 || plotWidth <= 0)
            return;

        var textBrush = TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;
        var primaryBrush = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var gridBrush = TryFindResource("ControlStrokeColorDefaultBrush") as Brush ?? Brushes.DimGray;
        var accentBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
        var label = new TextBlock
        {
            Text = "全局",
            FontSize = 10,
            Foreground = textBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, 6);
        Canvas.SetTop(label, 7);
        OverviewCanvas.Children.Add(label);
        OverviewCanvas.Children.Add(new Line
        {
            X1 = TrackHeaderWidth,
            X2 = TrackHeaderWidth,
            Y1 = 0,
            Y2 = height,
            Stroke = gridBrush,
            Opacity = 0.62,
            StrokeThickness = 1
        });

        if (_videoDurationMilliseconds <= 0)
            return;

        var overviewSize = new Size(width, height);
        if (!ReferenceEquals(_overviewWaveformSource, _waveform) ||
            !_overviewWaveformSize.Equals(overviewSize))
        {
            _overviewWaveformGeometry = _waveform == null
                ? null
                : CreateWaveformGeometry(
                    _waveform,
                    0,
                    _videoDurationMilliseconds,
                    TrackHeaderWidth,
                    width,
                    2,
                    Math.Max(1, height - 4));
            _overviewWaveformSource = _waveform;
            _overviewWaveformSize = overviewSize;
        }

        if (_overviewWaveformGeometry != null)
        {
            OverviewCanvas.Children.Add(new Path
            {
                Data = _overviewWaveformGeometry,
                Stroke = primaryBrush,
                StrokeThickness = 1,
                Opacity = 0.20,
                IsHitTestVisible = false
            });
        }

        foreach (var item in _events)
        {
            var startX = TrackHeaderWidth +
                         GetStartMilliseconds(item) / (double)_videoDurationMilliseconds * plotWidth;
            var endX = TrackHeaderWidth +
                       GetEndMilliseconds(item) / (double)_videoDurationMilliseconds * plotWidth;
            var blockWidth = Math.Max(2, endX - startX);
            var block = new Rectangle
            {
                Width = blockWidth,
                Height = 6,
                Fill = item.AccentBrush,
                Opacity = ReferenceEquals(item, _selection) ? 0.95 : 0.62,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(block, startX);
            Canvas.SetTop(block, 3 + (int)item.Track * 8);
            OverviewCanvas.Children.Add(block);
        }

        var viewportStart = TrackHeaderWidth +
                            _viewStartMilliseconds / _videoDurationMilliseconds * plotWidth;
        var viewportEndTime = Math.Min(
            _videoDurationMilliseconds,
            _viewStartMilliseconds + GetVisibleMilliseconds());
        var viewportEnd = TrackHeaderWidth +
                          viewportEndTime / _videoDurationMilliseconds * plotWidth;
        var viewportFill = accentBrush is SolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = 0.16 }
            : new SolidColorBrush(Color.FromArgb(42, 0, 120, 212));
        var viewport = new Rectangle
        {
            Width = Math.Max(3, viewportEnd - viewportStart),
            Height = height - 2,
            Fill = viewportFill,
            Stroke = accentBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(viewport, viewportStart);
        Canvas.SetTop(viewport, 1);
        OverviewCanvas.Children.Add(viewport);
    }

    private void DrawWaveform(
        Canvas canvas,
        double startMilliseconds,
        double endMilliseconds,
        double left,
        double right,
        double top,
        double height,
        Brush brush,
        double opacity)
    {
        var waveform = _waveform;
        if (waveform == null)
            return;

        var geometry = CreateWaveformGeometry(
            waveform, startMilliseconds, endMilliseconds, left, right, top, height);
        if (geometry == null)
            return;
        canvas.Children.Add(new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = 1,
            Opacity = opacity,
            IsHitTestVisible = false
        });
    }

    private static StreamGeometry? CreateWaveformGeometry(
        AudioWaveformEnvelope waveform,
        double startMilliseconds,
        double endMilliseconds,
        double left,
        double right,
        double top,
        double height)
    {
        if (endMilliseconds <= startMilliseconds || right <= left || height <= 0)
            return null;

        var pixelCount = Math.Max(1, (int)Math.Ceiling(right - left));
        var millisecondsPerPixel = (endMilliseconds - startMilliseconds) / pixelCount;
        var center = top + height / 2;
        var amplitude = height * 0.48 / 32768d;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                var pixelStart = startMilliseconds + pixel * millisecondsPerPixel;
                var pixelEnd = pixelStart + millisecondsPerPixel;
                var firstBucket = Math.Max(0, (int)Math.Floor(pixelStart / waveform.BucketMilliseconds));
                var lastBucket = Math.Min(
                    waveform.Minimum.Length - 1,
                    (int)Math.Ceiling(pixelEnd / waveform.BucketMilliseconds) - 1);
                if (firstBucket > lastBucket || firstBucket >= waveform.Minimum.Length)
                    continue;

                var minimum = short.MaxValue;
                var maximum = short.MinValue;
                for (var bucket = firstBucket; bucket <= lastBucket; bucket++)
                {
                    minimum = Math.Min(minimum, waveform.Minimum[bucket]);
                    maximum = Math.Max(maximum, waveform.Maximum[bucket]);
                }

                var x = left + pixel + 0.5;
                context.BeginFigure(new Point(x, center - maximum * amplitude), false, false);
                context.LineTo(new Point(x, center - minimum * amplitude), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private double GetMajorTickInterval()
    {
        double[] candidates =
        [
            50, 100, 200, 500, 1000, 2000, 5000, 10000, 30000, 60000,
            120000, 300000, 600000, 1800000, 3600000
        ];
        return candidates.FirstOrDefault(interval => interval * _pixelsPerSecond / 1000 >= 72, candidates[^1]);
    }

    private int PointToFrame(double x)
    {
        if (_selection == null)
            return 0;
        return Math.Max(0, _selection.FrameRate.FrameAtTime((int)Math.Round(XToTime(x))));
    }

    private double TimeToX(double milliseconds)
    {
        return TrackHeaderWidth + (milliseconds - _viewStartMilliseconds) * _pixelsPerSecond / 1000;
    }

    private double XToTime(double x)
    {
        return _viewStartMilliseconds + Math.Max(0, x - TrackHeaderWidth) * 1000 / _pixelsPerSecond;
    }

    private double GetVisibleMilliseconds()
    {
        var plotWidth = GetPlotWidth();
        return plotWidth <= 0
            ? 10000
            : plotWidth * 1000 / _pixelsPerSecond;
    }

    private double GetPlotWidth()
    {
        return Math.Max(0, TimelineCanvas.ActualWidth - TrackHeaderWidth);
    }

    private double GetOverviewPlotWidth()
    {
        return Math.Max(0, OverviewCanvas.ActualWidth - TrackHeaderWidth);
    }

    private static int GetStartMilliseconds(TimelineEventSelection selection)
    {
        return selection.FrameRate.TimeAtFrame(selection.StartFrame, FrameType.Start).Milliseconds;
    }

    private static int GetEndMilliseconds(TimelineEventSelection selection)
    {
        return selection.FrameRate.TimeAtFrame(selection.EndFrame, FrameType.End).Milliseconds;
    }

    private static string FormatTimestamp(int milliseconds)
    {
        milliseconds = Math.Max(0, milliseconds);
        var hours = milliseconds / 3600000;
        var minutes = milliseconds / 60000 % 60;
        var seconds = milliseconds / 1000 % 60;
        var centiseconds = milliseconds / 10 % 100;
        return $"{hours}:{minutes:00}:{seconds:00}.{centiseconds:00}";
    }

    private static string FormatRulerTimestamp(int milliseconds)
    {
        milliseconds = Math.Max(0, milliseconds);
        var hours = milliseconds / 3600000;
        var minutes = milliseconds / 60000 % 60;
        var seconds = milliseconds / 1000 % 60;
        var centiseconds = milliseconds / 10 % 100;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}.{centiseconds:00}"
            : $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }

    private static bool TryParseTimestamp(string value, out int milliseconds)
    {
        milliseconds = 0;
        var match = TimestampPattern().Match(value.Trim());
        if (!match.Success ||
            !int.TryParse(match.Groups["hour"].Value, out var hour) ||
            !int.TryParse(match.Groups["minute"].Value, out var minute) ||
            !int.TryParse(match.Groups["second"].Value, out var second) ||
            minute >= 60 || second >= 60)
            return false;

        var fractionText = match.Groups["fraction"].Value;
        var fraction = fractionText.Length switch
        {
            0 => 0,
            1 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 100,
            2 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 10,
            _ => int.Parse(fractionText[..3], CultureInfo.InvariantCulture)
        };

        var total = (long)hour * 3600000 + minute * 60000L + second * 1000L + fraction;
        if (total > int.MaxValue)
            return false;

        milliseconds = (int)total;
        return true;
    }

    [GeneratedRegex(@"^(?<hour>\d{1,2}):(?<minute>\d{2}):(?<second>\d{2})(?:[\.,](?<fraction>\d{1,3}))?$")]
    private static partial Regex TimestampPattern();

    private enum DragMode
    {
        None,
        Start,
        End,
        Range
    }

    private sealed record TimingEditCommand(
        TimelineEventSelection Selection,
        int OldStartFrame,
        int OldEndFrame,
        int NewStartFrame,
        int NewEndFrame);

    private sealed record TimelineHitRegion(TimelineEventSelection Selection, Rect Bounds);
}
