using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class ClipBase(Point from, Point to) : Tag, INestableTag
{
    protected ClipBase(int fromX, int fromY, int toX, int toY) : this(new Point(fromX, fromY), new Point(toX, toY))
    {
    }

    protected ClipBase(Rectangle rect) : this(rect.Location, rect.Location + rect.Size)
    {
    }

    public Point From { get; } = from;
    public Point To { get; } = to;

    public abstract override string Name { get; }

    public override string ToString()
    {
        return $"\\{Name}({From.X},{From.Y},{To.X},{To.Y})";
    }
}

public class Clip : ClipBase
{
    public Clip(Point from, Point to) : base(from, to)
    {
    }

    public Clip(int fromX, int fromY, int toX, int toY) : base(fromX, fromY, toX, toY)
    {
    }

    public Clip(Rectangle rect) : base(rect)
    {
    }

    public override string Name => "clip";
}

public class ClipInverse : ClipBase
{
    public ClipInverse(Point from, Point to) : base(from, to)
    {
    }

    public ClipInverse(int fromX, int fromY, int toX, int toY) : base(fromX, fromY, toX, toY)
    {
    }

    public ClipInverse(Rectangle rect) : base(rect)
    {
    }

    public override string Name => "iclip";
}

public abstract class ClipVectorBase : Tag
{
    protected ClipVectorBase(AssDraw.AssDraw vector)
    {
        Vector = vector;
    }

    protected ClipVectorBase(AssDraw.AssDraw vector, int scale)
    {
        Vector = vector;
        Scale = scale;
    }

    public AssDraw.AssDraw Vector { get; }

    public int Scale { get; } = 1;
    public abstract override string Name { get; }


    public override string ToString()
    {
        return Scale == 1 ? $"\\{Name}({Vector})" : $"\\{Name}({Scale},{Vector})";
    }
}

public class ClipVector : ClipVectorBase
{
    public ClipVector(AssDraw.AssDraw vector) : base(vector)
    {
    }


    public ClipVector(AssDraw.AssDraw vector, int scale) : base(vector, scale)
    {
    }

    public override string Name => "clip";
}

public class ClipInverseVector : ClipVectorBase
{
    public ClipInverseVector(AssDraw.AssDraw vector) : base(vector)
    {
    }

    public ClipInverseVector(AssDraw.AssDraw vector, int scale) : base(vector, scale)
    {
    }

    public override string Name => "iclip";
}