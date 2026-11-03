using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SekaiToolsGUI.Behaviors;

public static class NewlineMarker
{
    public static readonly DependencyProperty StyleProperty = DependencyProperty.RegisterAttached(
        "Style", typeof(int), typeof(NewlineMarker), new PropertyMetadata(0, Changed));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(MarkerState), typeof(NewlineMarker));

    public static int GetStyle(DependencyObject target) => (int)target.GetValue(StyleProperty);
    public static void SetStyle(DependencyObject target, int value) => target.SetValue(StyleProperty, value);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox box) return;
        if (box.GetValue(StateProperty) is not MarkerState state)
        {
            state = new MarkerState(box);
            box.SetValue(StateProperty, state);
        }
        state.Refresh();
    }

    private sealed class MarkerState
    {
        private readonly TextBox _box;
        private MarkerAdorner? _adorner;
        private AdornerLayer? _layer;

        public MarkerState(TextBox box)
        {
            _box = box;
            box.Loaded += (_, _) => Refresh();
            box.Unloaded += (_, _) => Remove();
            box.TextChanged += (_, _) => Refresh();
            box.LayoutUpdated += (_, _) => _adorner?.UpdateGeometry();
            box.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler((_, _) => _adorner?.InvalidateVisual()), true);
        }

        public void Refresh()
        {
            if (GetStyle(_box) is not (1 or 2) || _box.Text.IndexOfAny(['\r', '\n']) < 0)
            {
                Remove();
                return;
            }
            if (!_box.IsLoaded) return;
            if (_adorner == null)
            {
                _layer = AdornerLayer.GetAdornerLayer(_box);
                if (_layer == null) return;
                _adorner = new MarkerAdorner(_box);
                _layer.Add(_adorner);
            }
            _adorner.InvalidateVisual();
        }

        private void Remove()
        {
            if (_adorner != null) _layer?.Remove(_adorner);
            _adorner = null;
            _layer = null;
        }
    }

    private sealed class MarkerAdorner : Adorner
    {
        private readonly TextBox _box;
        private Rect[] _positions = [];
        private Size _size;

        public void UpdateGeometry()
        {
            var positions = Enumerable.Range(0, _box.Text.Length)
                .Where(index => _box.Text[index] is '\r' or '\n')
                .Select(index => _box.GetRectFromCharacterIndex(index)).ToArray();
            if (_positions.SequenceEqual(positions) && _size == _box.RenderSize) return;
            _positions = positions;
            _size = _box.RenderSize;
            InvalidateVisual();
        }

        public MarkerAdorner(TextBox box) : base(box)
        {
            _box = box;
            IsHitTestVisible = false;
            Focusable = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var brush = _box.Foreground.CloneCurrentValue();
            brush.Opacity *= 0.35;
            var glyph = new FormattedText(GetStyle(_box) == 1 ? @"\N" : "↵",
                CultureInfo.CurrentUICulture, _box.FlowDirection,
                new Typeface(_box.FontFamily, _box.FontStyle, _box.FontWeight, _box.FontStretch),
                _box.FontSize * 0.8, brush, VisualTreeHelper.GetDpi(_box).PixelsPerDip);
            // Clip to the text viewport rather than drawing over borders or scrollbars.
            var viewport = _box.Template.FindName("PART_ContentHost", _box) as FrameworkElement;
            var clip = viewport == null ? new Rect(_box.RenderSize)
                : new Rect(viewport.TranslatePoint(new Point(), _box), viewport.RenderSize);
            drawingContext.PushClip(new RectangleGeometry(clip));
            for (var index = 0; index < _box.Text.Length; index++)
            {
                if (_box.Text[index] is not ('\r' or '\n')) continue;
                var rectangle = _box.GetRectFromCharacterIndex(index);
                if (!rectangle.IsEmpty && rectangle.IntersectsWith(clip))
                    drawingContext.DrawText(glyph, new Point(rectangle.X + 2,
                        rectangle.Y + Math.Max(0, (rectangle.Height - glyph.Height) / 2)));
                if (_box.Text[index] == '\r' && index + 1 < _box.Text.Length && _box.Text[index + 1] == '\n')
                    index++;
            }
            drawingContext.Pop();
        }
    }
}
