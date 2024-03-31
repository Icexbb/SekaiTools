using System.Drawing;

namespace SekaiToolsCore.Process;

public struct MatchResult(double maxVal, double minVal, Point maxLoc, Point minLoc)
{
    public struct ValuePair(double value, Point location)
    {
        public double Value { get; set; } = value;
        public Point Location { get; set; } = location;
    }

    public double MaxVal { get; set; } = maxVal;
    public double MinVal { get; set; } = minVal;

    public Point MaxLoc { get; set; } = maxLoc;
    public Point MinLoc { get; set; } = minLoc;

    public ValuePair Max => new(MaxVal, MaxLoc);
    public ValuePair Min => new(MinVal, MinLoc);

    public MatchResult() : this(0, 0, Point.Empty, Point.Empty)
    {
    }
}