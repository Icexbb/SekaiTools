namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public abstract class GradientAlphaBase(
    SubtitleAlpha leftTop,
    SubtitleAlpha rightTop,
    SubtitleAlpha leftBottom,
    SubtitleAlpha rightBottom)
    : Tag, INestableTag
{
    public abstract override string Name { get; }

    public SubtitleAlpha LeftTop { get; } = leftTop;
    public SubtitleAlpha RightTop { get; } = rightTop;
    public SubtitleAlpha LeftBottom { get; } = leftBottom;
    public SubtitleAlpha RightBottom { get; } = rightBottom;

    public override string ToString() => $"\\{Name}({LeftTop},{RightTop},{LeftBottom},{RightBottom})";
}

public class PrimaryGradientAlpha(
    SubtitleAlpha leftTop,
    SubtitleAlpha rightTop,
    SubtitleAlpha leftBottom,
    SubtitleAlpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "1va";
}

public class SecondaryGradientAlpha(
    SubtitleAlpha leftTop,
    SubtitleAlpha rightTop,
    SubtitleAlpha leftBottom,
    SubtitleAlpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "2va";
}

public class OutlineGradientAlpha(
    SubtitleAlpha leftTop,
    SubtitleAlpha rightTop,
    SubtitleAlpha leftBottom,
    SubtitleAlpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "3va";
}

public class ShadowGradientAlpha(
    SubtitleAlpha leftTop,
    SubtitleAlpha rightTop,
    SubtitleAlpha leftBottom,
    SubtitleAlpha rightBottom)
    : GradientAlphaBase(leftTop, rightTop, leftBottom, rightBottom)
{
    public override string Name => "4va";
}