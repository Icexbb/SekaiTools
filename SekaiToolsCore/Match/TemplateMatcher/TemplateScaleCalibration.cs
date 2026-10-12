using SekaiToolsCore.Process;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class TemplateScaleCalibration
{
    private const double LockConfidence = 0.85;
    private const double UnlockConfidence = 0.50;
    private const int UnlockFailureCount = 30;
    private static readonly double[] InitialScales = [1.00, 0.96, 1.04];
    private double? _lockedScale;
    private readonly double[] _lockedScales = new double[1];

    private int _lowConfidenceCount;

    public IReadOnlyList<double> CandidateScales => _lockedScale.HasValue ? _lockedScales : InitialScales;

    public void Observe(double scale, double score)
    {
        if (_lockedScale == null)
        {
            if (double.IsFinite(score) && score >= LockConfidence)
            {
                _lockedScale = scale;
                _lockedScales[0] = scale;
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

    internal TemplateScaleStateDto CaptureState(int usage) => new(usage, _lockedScale, _lowConfidenceCount);

    internal static void ValidateState(TemplateScaleStateDto state)
    {
        if (state.Scale is { } scale && !InitialScales.Contains(scale)
            || state.LowConfidenceCount is < 0 or >= UnlockFailureCount
            || state.Scale == null && state.LowConfidenceCount != 0)
            throw new InvalidDataException("Invalid template scale checkpoint");
    }

    internal void RestoreState(TemplateScaleStateDto state)
    {
        ValidateState(state);
        _lockedScale = state.Scale;
        if (state.Scale is { } scale) _lockedScales[0] = scale;
        _lowConfidenceCount = state.LowConfidenceCount;
    }
}
