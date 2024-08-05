namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public abstract class GradientsColorBase(
    SubStationAlpha.Color leftTop,
    SubStationAlpha.Color rightTop,
    SubStationAlpha.Color leftBottom,
    SubStationAlpha.Color rightBottom)
    : Tag, INestableTag
{
    public abstract override string Name { get; }

    public SubStationAlpha.Color LeftTop { get; } = leftTop;
    public SubStationAlpha.Color RightTop { get; } = rightTop;
    public SubStationAlpha.Color LeftBottom { get; } = leftBottom;
    public SubStationAlpha.Color RightBottom { get; } = rightBottom;

    public override string ToString()
    {
        return $"\\{Name}({LeftTop},{RightTop},{LeftBottom},{RightBottom})";
    }
}

public class PrimaryGradientsColor(
    SubStationAlpha.Color leftTop,
    SubStationAlpha.Color rightTop,
    SubStationAlpha.Color leftBottom,
    SubStationAlpha.Color rightBottom)
    : GradientsColorBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "1vc";
}

public class SecondaryGradientsColor(
    SubStationAlpha.Color leftTop,
    SubStationAlpha.Color rightTop,
    SubStationAlpha.Color leftBottom,
    SubStationAlpha.Color rightBottom)
    : GradientsColorBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "2vc";
}

public class OutlineGradientsColor(
    SubStationAlpha.Color leftTop,
    SubStationAlpha.Color rightTop,
    SubStationAlpha.Color leftBottom,
    SubStationAlpha.Color rightBottom)
    : GradientsColorBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "3vc";
}

public class ShadowGradientsColor(
    SubStationAlpha.Color leftTop,
    SubStationAlpha.Color rightTop,
    SubStationAlpha.Color leftBottom,
    SubStationAlpha.Color rightBottom)
    : GradientsColorBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "4vc";
}