using System.Drawing;

namespace SekaiToolsCore.SubStationAlpha.AssDraw;

public class AssDraw(params AssDrawPart[] parts) : AssDrawPart("")
{
    private readonly AssDrawPart[] _parts = parts;


    public AssDraw(string source) : this()
    {
        source = source.ToLower();
        var sourceParts = source.Split(' ');
        var partsCount = sourceParts.Count(c => c is "m" or "l" or "b");
        var partList = new List<AssDrawPart>();
        for (var i = 0; i < sourceParts.Length; i++)
            switch (source[i])
            {
                case 'm':
                {
                    var x = int.Parse(sourceParts[i + 1]);
                    var y = int.Parse(sourceParts[i + 2]);
                    AssDrawMove move = new(new AssDrawPoint(x, y));
                    partList.Add(move);
                }
                    i += 2;
                    break;
                case 'l':
                {
                    var x = int.Parse(sourceParts[i + 1]);
                    var y = int.Parse(sourceParts[i + 2]);
                    AssDrawLine line = new(new AssDrawPoint(x, y));
                    partList.Add(line);
                }
                    i += 2;
                    break;
                case 'b':
                {
                    var x1 = int.Parse(sourceParts[i + 1]);
                    var y1 = int.Parse(sourceParts[i + 2]);
                    var x2 = int.Parse(sourceParts[i + 3]);
                    var y2 = int.Parse(sourceParts[i + 4]);
                    var x3 = int.Parse(sourceParts[i + 5]);
                    var y3 = int.Parse(sourceParts[i + 6]);
                    var bezier = new Bezier(new AssDrawPoint(x1, y1), new AssDrawPoint(x2, y2),
                        new AssDrawPoint(x3, y3));
                    partList.Add(bezier);
                }
                    i += 6;
                    break;
            }

        if (partsCount != partList.Count) throw new Exception("Draw Part Count Not Match");
        _parts = partList.ToArray();
    }

    public override string ToString()
    {
        var result = _parts.Aggregate("", (current, p) => $"{current} {p}");
        return result.Trim();
    }

    public override void Move(int x, int y)
    {
        foreach (var t in _parts) t.Move(x, y);
    }

    public override void Scale(float ratio)
    {
        foreach (var t in _parts) t.Scale(ratio);
    }

    public override void Scale(double ratio)
    {
        foreach (var t in _parts) t.Scale(ratio);
    }

    public static AssDraw Rectangle(Rectangle rect)
    {
        var parts = new List<AssDrawPart>
        {
            new AssDrawMove(rect.Location),
            new AssDrawLine(rect.Location + new Size(rect.Width, 0)),
            new AssDrawLine(rect.Location + new Size(rect.Width, rect.Height)),
            new AssDrawLine(rect.Location + new Size(0, rect.Height))
        };

        return new AssDraw(parts.ToArray());
    }

    public static Append Append(Point point)
    {
        return new Append(point.X, point.Y);
    }

    public static AssDrawLine Line(Point point)
    {
        return new AssDrawLine(point.X, point.Y);
    }

    public static AssDrawMove Move(Point point)
    {
        return new AssDrawMove(point.X, point.Y);
    }

    public static Bezier Bezier(Point pointA, Point pointB, Point pointC)
    {
        return new Bezier(pointA.X, pointA.Y, pointB.X, pointB.Y, pointC.X, pointC.Y);
    }

    public static AssDrawClose Close()
    {
        return new AssDrawClose();
    }

    public static Successive Successive(params Point[] parts)
    {
        return new Successive(parts.Select(p => new AssDrawPoint(p.X, p.Y)).ToArray());
    }
}