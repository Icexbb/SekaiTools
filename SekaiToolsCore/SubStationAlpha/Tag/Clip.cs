using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public abstract class ClipBase(Point from, Point to) : Tag, INestableTag
{
    public Point From { get; } = from;
    public Point To { get; } = to;

    public abstract override string Name { get; }

    protected ClipBase(int fromX, int fromY, int toX, int toY) : this(new Point(fromX, fromY), new Point(toX, toY))
    {
    }

    protected ClipBase(Rectangle rect) : this(rect.Location, rect.Location + rect.Size)
    {
    }

    public override string ToString() => $"\\{Name}({From.X},{From.Y},{To.X},{To.Y})";
}

public class Clip : ClipBase
{
    public override string Name => "clip";

    public Clip(Point from, Point to) : base(from, to)
    {
    }

    public Clip(int fromX, int fromY, int toX, int toY) : base(fromX, fromY, toX, toY)
    {
    }

    public Clip(Rectangle rect) : base(rect)
    {
    }
}

public class ClipInverse : ClipBase
{
    public override string Name => "iclip";

    public ClipInverse(Point from, Point to) : base(from, to)
    {
    }

    public ClipInverse(int fromX, int fromY, int toX, int toY) : base(fromX, fromY, toX, toY)
    {
    }

    public ClipInverse(Rectangle rect) : base(rect)
    {
    }
}

public abstract class ClipVectorBase : Tag
{
    public AssDraw.AssDraw Vector { get; }

    public int Scale { get; } = 1;
    public abstract override string Name { get; }

    protected ClipVectorBase(AssDraw.AssDraw vector)
    {
        Vector = vector;
    }

    protected ClipVectorBase(AssDraw.AssDraw vector, int scale)
    {
        Vector = vector;
        Scale = scale;
    }


    public override string ToString() => Scale == 1 ? $"\\{Name}({Vector})" : $"\\{Name}({Scale},{Vector})";
}

public class ClipVector : ClipVectorBase
{
    public override string Name => "clip";

    public ClipVector(AssDraw.AssDraw vector) : base(vector)
    {
    }


    public ClipVector(AssDraw.AssDraw vector, int scale) : base(vector, scale)
    {
    }
}

public class ClipInverseVector : ClipVectorBase
{
    public override string Name => "iclip";

    public ClipInverseVector(AssDraw.AssDraw vector) : base(vector)
    {
    }

    public ClipInverseVector(AssDraw.AssDraw vector, int scale) : base(vector, scale)
    {
    }
}