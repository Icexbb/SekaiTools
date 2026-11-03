using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsGUI.Behaviors;

public static class TextDifferenceHighlight
{
    public static readonly DependencyProperty ComparisonProperty = DependencyProperty.RegisterAttached(
        "Comparison", typeof(string), typeof(TextDifferenceHighlight), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TextDifferenceHighlight), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty LoadedComparisonProperty = DependencyProperty.RegisterAttached(
        "LoadedComparison", typeof(string), typeof(TextDifferenceHighlight), new PropertyMetadata(null, Changed));
    public static string? GetLoadedComparison(DependencyObject target) => (string?)target.GetValue(LoadedComparisonProperty);
    public static void SetLoadedComparison(DependencyObject target, string? value) => target.SetValue(LoadedComparisonProperty, value);

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(HighlightState), typeof(TextDifferenceHighlight));

    public static string? GetComparison(DependencyObject target) => (string?)target.GetValue(ComparisonProperty);
    public static void SetComparison(DependencyObject target, string? value) => target.SetValue(ComparisonProperty, value);
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox box) return;
        if (box.GetValue(StateProperty) is not HighlightState state)
        {
            state = new HighlightState(box);
            box.SetValue(StateProperty, state);
        }
        state.Refresh();
    }

    private sealed class HighlightState
    {
        private readonly TextBox _box;
        private DifferenceAdorner? _adorner;
        private AdornerLayer? _layer;
        private IReadOnlyList<TextDifferenceRange> _ranges = [];
        private IReadOnlyList<TextDifferenceRange> _loadedRanges = [];
        private bool _queued;

        public HighlightState(TextBox box)
        {
            _box = box;
            box.Loaded += (_, _) => Refresh();
            box.Unloaded += (_, _) => Remove();
            box.TextChanged += (_, _) => Refresh();
            box.LayoutUpdated += (_, _) => _adorner?.UpdateGeometry();
            box.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler((_, _) => _adorner?.UpdateGeometry()), true);
        }

        public void Refresh()
        {
            var comparison = GetComparison(_box);
            _ranges = GetIsEnabled(_box) && comparison != null
                ? TextDifference.Compare(_box.Text, comparison).Current
                : [];
            var loaded = GetLoadedComparison(_box);
            _loadedRanges = loaded != null ? TextDifference.Compare(_box.Text, loaded).Current : [];
            if (_ranges.Count == 0 && _loadedRanges.Count == 0)
            {
                Remove();
                return;
            }
            if (_queued || !_box.IsLoaded) return;
            _queued = true;
            _box.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _queued = false;
                if (!_box.IsLoaded || (_ranges.Count == 0 && _loadedRanges.Count == 0)) return;
                if (_adorner == null)
                {
                    _layer = AdornerLayer.GetAdornerLayer(_box);
                    if (_layer == null) return;
                    _adorner = new DifferenceAdorner(_box);
                    _layer.Add(_adorner);
                }
                _adorner.Ranges = _ranges;
                _adorner.LoadedRanges = _loadedRanges;
                _adorner.UpdateGeometry();
            }));
        }

        private void Remove()
        {
            if (_adorner != null) _layer?.Remove(_adorner);
            _adorner = null;
            _layer = null;
        }
    }

    private sealed class DifferenceAdorner : Adorner
    {
        private static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
            "HighlightBrush", typeof(Brush), typeof(DifferenceAdorner),
            new FrameworkPropertyMetadata(Brushes.DarkOrange, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty LoadedBrushProperty = DependencyProperty.Register(
            "LoadedBrush", typeof(Brush), typeof(DifferenceAdorner),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
        private readonly TextBox _box;
        private List<Rect> _loadedRectangles = [];
        public IReadOnlyList<TextDifferenceRange> LoadedRanges { get; set; } = [];
        private List<Rect> _rectangles = [];
        public IReadOnlyList<TextDifferenceRange> Ranges { get; set; } = [];

        public DifferenceAdorner(TextBox box) : base(box)
        {
            _box = box;
            if (SystemParameters.HighContrast)
                SetResourceReference(LoadedBrushProperty, SystemColors.HighlightBrushKey);
            IsHitTestVisible = false;
            SetResourceReference(HighlightBrushProperty, SystemParameters.HighContrast
                ? SystemColors.HighlightBrushKey
                : "SystemFillColorCautionBrush");
        }

        public void UpdateGeometry()
        {
            var reference = BuildRectangles(Ranges);
            var loaded = BuildRectangles(LoadedRanges);
            if (_rectangles.SequenceEqual(reference) && _loadedRectangles.SequenceEqual(loaded)) return;
            _rectangles = reference;
            _loadedRectangles = loaded;
            InvalidateVisual();
        }

        private List<Rect> BuildRectangles(IReadOnlyList<TextDifferenceRange> ranges)
        {
            var box = _box;
            var rectangles = new List<Rect>();
            foreach (var range in ranges)
            {
                if (range.Length == 0)
                {
                    var marker = box.GetRectFromCharacterIndex(Math.Min(range.Start, box.Text.Length));
                    if (marker.IsEmpty && box.Text.Length == 0)
                        marker = new Rect(box.Padding.Left + 2, box.Padding.Top + 2, 3, box.FontSize * 1.4);
                    if (!marker.IsEmpty) rectangles.Add(new Rect(marker.X, marker.Y, 3, marker.Height));
                    continue;
                }
                for (var index = range.Start; index < range.Start + range.Length && index < box.Text.Length;)
                {
                    var length = StringInfo.GetNextTextElementLength(box.Text, index);
                    var start = box.GetRectFromCharacterIndex(index);
                    var end = box.GetRectFromCharacterIndex(index + length - 1, true);
                    var newline = box.Text[index] is '\r' or '\n';
                    index += length;
                    if (start.IsEmpty || end.IsEmpty) continue;
                    var rectangle = new Rect(newline ? start.X : Math.Min(start.X, end.X), start.Y,
                        newline ? 3 : Math.Max(3, Math.Abs(end.X - start.X)), start.Height);
                    if (rectangles.Count > 0 && Math.Abs(rectangles[^1].Y - rectangle.Y) < 0.5 &&
                        Math.Abs(rectangles[^1].Right - rectangle.Left) < 1)
                    {
                        var merged = rectangles[^1];
                        merged.Union(rectangle);
                        rectangles[^1] = merged;
                    }
                    else rectangles.Add(rectangle);
                }
            }
            return rectangles;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var brush = (Brush)GetValue(HighlightBrushProperty);
            var fill = brush.CloneCurrentValue();
            fill.Opacity = 0.18;
            drawingContext.PushClip(new RectangleGeometry(new Rect(_box.RenderSize)));
            foreach (var rectangle in _rectangles)
                drawingContext.DrawRoundedRectangle(fill, new Pen(brush, 1), rectangle, 2, 2);
            var loadedBrush = (Brush)GetValue(LoadedBrushProperty);
            foreach (var rectangle in _loadedRectangles)
                drawingContext.DrawRoundedRectangle(loadedBrush, null,
                    new Rect(rectangle.X, rectangle.Bottom - 3, Math.Max(3, rectangle.Width), 3), 1, 1);
            drawingContext.Pop();
        }
    }
}
