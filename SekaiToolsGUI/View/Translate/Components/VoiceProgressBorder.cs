using System.Windows;
using System.Windows.Media;

namespace SekaiToolsGUI.View.Translate.Components;

/// <summary>Draws elapsed playback along the button perimeter without intercepting input.</summary>
public sealed class VoiceProgressBorder : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(VoiceProgressBorder),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(VoiceProgressBorder),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!double.IsFinite(Progress) || Progress <= 0 || ActualWidth <= 2 || ActualHeight <= 2) return;
        var outline = new RectangleGeometry(new Rect(1, 1, ActualWidth - 2, ActualHeight - 2), 4, 4)
            .GetOutlinedPathGeometry();
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            outline.GetPointAtFractionLength(0, out var start, out _);
            context.BeginFigure(start, false, false);
            var progress = Math.Clamp(Progress, 0, 1);
            var steps = (int)Math.Ceiling(progress * 160);
            for (var step = 1; step <= steps; step++)
            {
                outline.GetPointAtFractionLength(progress * step / steps, out var point, out _);
                context.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(Stroke, 2), geometry);
    }
}