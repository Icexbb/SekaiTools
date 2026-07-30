namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class TemplateScaleCalibration
{
    private static readonly double[] InitialScales = [1.00, 0.96, 1.04];
    private const double LockConfidence = 0.85;
    private const double UnlockConfidence = 0.50;
    private const int UnlockFailureCount = 30;

    private int _lowConfidenceCount;
    private double? _lockedScale;

    public IReadOnlyList<double> CandidateScales => _lockedScale is { } scale ? [scale] : InitialScales;

    public void Observe(double scale, double score)
    {
        if (_lockedScale == null)
        {
            if (double.IsFinite(score) && score >= LockConfidence)
            {
                _lockedScale = scale;
                _lowConfidenceCount = 0;
            }
            return;
        }

        if (double.IsFinite(score) && score >= UnlockConfidence)
        {
            _lowConfidenceCount = 0;
            return;
        }

        _lowConfidenceCount++;
        if (_lowConfidenceCount < UnlockFailureCount) return;

        _lockedScale = null;
        _lowConfidenceCount = 0;
    }

    public void Reset()
    {
        _lockedScale = null;
        _lowConfidenceCount = 0;
    }
}
