namespace SekaiToolsBase.SubStationAlpha.Tag;

public enum AnchorPos
{
    BottomLeft = 1,
    BottomCenter = 2,
    BottomRight = 3,
    MiddleLeft = 4,
    MiddleCenter = 5,
    MiddleRight = 6,
    TopLeft = 7,
    TopCenter = 8,
    TopRight = 9
}

public class Anchor(AnchorPos pos) : Tag
{
    public Anchor(int pos) : this(AnchorPos.BottomCenter)
    {
        if (pos is > 9 or < 1)
            throw new ArgumentOutOfRangeException(nameof(pos), "The position must be between 1 and 9.");
        Pos = (AnchorPos)pos;
    }

    public AnchorPos Pos { get; } = pos;

    public override string Name => "an";

    public override string ToString()
    {
        return $"\\{Name}{(int)Pos}";
    }
}

public enum AnchorTraditionalPos
{
    BottomLeft = 1,
    BottomCenter = 2,
    BottomRight = 3,
    TopLeft = 5,
    TopCenter = 6,
    TopRight = 7,
    MiddleLeft = 9,
    MiddleCenter = 10,
    MiddleRight = 11
}

public class AnchorTraditional(AnchorTraditionalPos pos) : Tag
{
    public AnchorTraditional(int pos) : this(AnchorTraditionalPos.BottomCenter)
    {
        if (pos is not (1 or 2 or 3 or 5 or 6 or 7 or 9 or 10 or 11))
            throw new ArgumentOutOfRangeException(nameof(pos), "The position must be 1 or 2.");

        Pos = (AnchorTraditionalPos)pos;
    }

    public AnchorTraditionalPos Pos { get; } = pos;

    public override string Name => "a";

    public override string ToString()
    {
        return $"\\{Name}{Pos}";
    }
}