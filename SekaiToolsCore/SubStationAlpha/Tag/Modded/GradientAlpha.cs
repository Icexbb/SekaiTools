namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public abstract class GradientAlphaBase(
    SubStationAlpha.Alpha leftTop,
    SubStationAlpha.Alpha rightTop,
    SubStationAlpha.Alpha leftBottom,
    SubStationAlpha.Alpha rightBottom)
    : Tag, INestableTag
{
    public abstract override string Name { get; }

    public SubStationAlpha.Alpha LeftTop { get; } = leftTop;
    public SubStationAlpha.Alpha RightTop { get; } = rightTop;
    public SubStationAlpha.Alpha LeftBottom { get; } = leftBottom;
    public SubStationAlpha.Alpha RightBottom { get; } = rightBottom;

    public override string ToString()
    {
        return $"\\{Name}({LeftTop},{RightTop},{LeftBottom},{RightBottom})";
    }
}

public class PrimaryGradientAlpha(
    SubStationAlpha.Alpha leftTop,
    SubStationAlpha.Alpha rightTop,
    SubStationAlpha.Alpha leftBottom,
    SubStationAlpha.Alpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "1va";
}

public class SecondaryGradientAlpha(
    SubStationAlpha.Alpha leftTop,
    SubStationAlpha.Alpha rightTop,
    SubStationAlpha.Alpha leftBottom,
    SubStationAlpha.Alpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "2va";
}

public class OutlineGradientAlpha(
    SubStationAlpha.Alpha leftTop,
    SubStationAlpha.Alpha rightTop,
    SubStationAlpha.Alpha leftBottom,
    SubStationAlpha.Alpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "3va";
}

public class ShadowGradientAlpha(
    SubStationAlpha.Alpha leftTop,
    SubStationAlpha.Alpha rightTop,
    SubStationAlpha.Alpha leftBottom,
    SubStationAlpha.Alpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "4va";
}