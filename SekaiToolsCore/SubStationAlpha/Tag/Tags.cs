using System.Drawing;
using SekaiToolsCore.SubStationAlpha.Tag.Modded;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public partial class Tags(params Tag[] tags)
{
    private readonly Tag[] _tags = tags;

    public override string ToString()
    {
        return "{" + string.Join("", _tags.Select(tag => tag.ToString())) + "}";
    }

    public static implicit operator Tags(Tag[] tags)
    {
        return new Tags(tags);
    }

    public static Tags FromITag(Tag tag)
    {
        return new Tags(tag);
    }

    public static Tags operator +(Tags tags, Tag tag)
    {
        return new Tags(tags._tags.Append(tag).ToArray());
    }

    public static Tags operator +(Tag tag, Tags tags)
    {
        return new Tags(tags._tags.Prepend(tag).ToArray());
    }

    public static Tags operator +(Tags tags1, Tags tags2)
    {
        return new Tags(tags1._tags.Concat(tags2._tags).ToArray());
    }

    public static string operator +(Tags tags, string text)
    {
        return tags.ToString() + text;
    }

    public static string operator +(string text, Tags tags)
    {
        return text + tags.ToString();
    }
}

public partial class Tags
{
    public static Alpha Alpha(int a)
    {
        return new Alpha(a);
    }

    public static Alpha Alpha(SubStationAlpha.Alpha a)
    {
        return new Alpha(a);
    }

    public static PrimaryAlpha PrimaryAlpha(int a)
    {
        return new PrimaryAlpha(a);
    }

    public static PrimaryAlpha PrimaryAlpha(SubStationAlpha.Alpha a)
    {
        return new PrimaryAlpha(a);
    }

    public static SecondaryAlpha SecondaryAlpha(int a)
    {
        return new SecondaryAlpha(a);
    }

    public static SecondaryAlpha SecondaryAlpha(SubStationAlpha.Alpha a)
    {
        return new SecondaryAlpha(a);
    }

    public static OutlineAlpha OutlineAlpha(int a)
    {
        return new OutlineAlpha(a);
    }

    public static OutlineAlpha OutlineAlpha(SubStationAlpha.Alpha a)
    {
        return new OutlineAlpha(a);
    }

    public static ShadowAlpha ShadowAlpha(int a)
    {
        return new ShadowAlpha(a);
    }

    public static ShadowAlpha ShadowAlpha(SubStationAlpha.Alpha a)
    {
        return new ShadowAlpha(a);
    }

    public static Anchor Anchor(AnchorPos pos)
    {
        return new Anchor(pos);
    }

    public static Anchor Anchor(int pos)
    {
        return new Anchor(pos);
    }

    public static AnchorTraditional AnchorTraditional(AnchorTraditionalPos pos)
    {
        return new AnchorTraditional(pos);
    }

    public static AnchorTraditional AnchorTraditional(int pos)
    {
        return new AnchorTraditional(pos);
    }

    public static Be Be(int value)
    {
        return new Be(value);
    }

    public static Blur Blur(int value)
    {
        return new Blur(value);
    }

    public static Bold Bold(int value)
    {
        return new Bold(value);
    }

    public static Clip Clip(int x1, int y1, int x2, int y2)
    {
        return new Clip(x1, y1, x2, y2);
    }

    public static Clip Clip(Point from, Point to)
    {
        return new Clip(from, to);
    }

    public static Clip Clip(Rectangle rect)
    {
        return new Clip(rect);
    }

    public static ClipInverse ClipInverse(int x1, int y1, int x2, int y2)
    {
        return new ClipInverse(x1, y1, x2, y2);
    }

    public static ClipInverse ClipInverse(Point from, Point to)
    {
        return new ClipInverse(from, to);
    }

    public static ClipInverse ClipInverse(Rectangle rect)
    {
        return new ClipInverse(rect);
    }

    public static ClipVector ClipVector(AssDraw.AssDraw ad, int scale = 1)
    {
        return new ClipVector(ad, scale);
    }

    public static ClipInverseVector ClipInverseVector(AssDraw.AssDraw ad, int scale = 1)
    {
        return new ClipInverseVector(ad, scale);
    }

    public static Color Color(SubStationAlpha.Color color)
    {
        return new Color(color);
    }

    public static Color Color(int r, int g, int b)
    {
        return new Color(r, g, b);
    }

    public static PrimaryColor PrimaryColor(SubStationAlpha.Color color)
    {
        return new PrimaryColor(color);
    }

    public static PrimaryColor PrimaryColor(int r, int g, int b)
    {
        return new PrimaryColor(r, g, b);
    }

    public static SecondaryColor SecondaryColor(SubStationAlpha.Color color)
    {
        return new SecondaryColor(color);
    }

    public static SecondaryColor SecondaryColor(int r, int g, int b)
    {
        return new SecondaryColor(r, g, b);
    }

    public static OutlineColor OutlineColor(SubStationAlpha.Color color)
    {
        return new OutlineColor(color);
    }

    public static OutlineColor OutlineColor(int r, int g, int b)
    {
        return new OutlineColor(r, g, b);
    }

    public static ShadowColor ShadowColor(SubStationAlpha.Color color)
    {
        return new ShadowColor(color);
    }

    public static ShadowColor ShadowColor(int r, int g, int b)
    {
        return new ShadowColor(r, g, b);
    }

    public static Fade Fade(int start, int end)
    {
        return new Fade(start, end);
    }

    public static FadeComplex FadeComplex(int alpha1, int alpha2, int alpha3, int time1, int time2, int time3,
        int time4)
    {
        return new FadeComplex(alpha1, alpha2, alpha3, time1, time2, time3, time4);
    }

    public static FontEncoding FontEncoding(int type)
    {
        return new FontEncoding(type);
    }

    public static FontName FontName(string name)
    {
        return new FontName(name);
    }

    public static FontRotate FontRotate(int angle)
    {
        return new FontRotate(angle);
    }

    public static FontRotateX FontRotateX(int angle)
    {
        return new FontRotateX(angle);
    }

    public static FontRotateY FontRotateY(int angle)
    {
        return new FontRotateY(angle);
    }

    public static FontRotateZ FontRotateZ(int angle)
    {
        return new FontRotateZ(angle);
    }

    public static FontScaleX FontScaleX(int value)
    {
        return new FontScaleX(value);
    }

    public static FontScaleY FontScaleY(int value)
    {
        return new FontScaleY(value);
    }

    public static FontShearingX FontShearingX(int value)
    {
        return new FontShearingX(value);
    }

    public static FontShearingY FontShearingY(int value)
    {
        return new FontShearingY(value);
    }

    public static FontSize FontSize(int value)
    {
        return new FontSize(value);
    }

    public static FontSpacing FontSpacing(int value)
    {
        return new FontSpacing(value);
    }

    public static Italic Italic(bool value)
    {
        return new Italic(value);
    }

    public static Karaoke Karaoke(int duration)
    {
        return new Karaoke(duration);
    }

    public static KaraokeFade KaraokeFade(int duration)
    {
        return new KaraokeFade(duration);
    }

    public static KaraokeOutline KaraokeOutline(int duration)
    {
        return new KaraokeOutline(duration);
    }

    public static LineBreak LineBreak(LineBreakStyle style)
    {
        return new LineBreak(style);
    }

    public static Move Move(int x1, int y1, int x2, int y2, int t1 = 0, int t2 = 0)
    {
        return new Move(x1, y1, x2, y2, t1, t2);
    }

    public static Move Move(Point from, Point to, int t1 = 0, int t2 = 0)
    {
        return new Move(from, to, t1, t2);
    }

    public static Paint Paint(int type)
    {
        return new Paint(type);
    }

    public static Position Position(int x, int y)
    {
        return new Position(x, y);
    }

    public static Position Position(Point point)
    {
        return new Position(point);
    }

    public static Reset Reset(string style = "")
    {
        return new Reset(style);
    }

    public static RotateCenter RotateCenter(int x, int y)
    {
        return new RotateCenter(x, y);
    }

    public static RotateCenter RotateCenter(Point point)
    {
        return new RotateCenter(point);
    }

    public static Bord Bord(int value)
    {
        return new Bord(value);
    }

    public static XBord XBord(int value)
    {
        return new XBord(value);
    }

    public static YBord YBord(int value)
    {
        return new YBord(value);
    }

    public static Shad Shad(int value)
    {
        return new Shad(value);
    }

    public static XShad XShad(int value)
    {
        return new XShad(value);
    }

    public static YShad YShad(int value)
    {
        return new YShad(value);
    }

    public static StrikeOut StrikeOut(bool value)
    {
        return new StrikeOut(value);
    }

    public static Transformation Transformation(params INestableTag[] tag)
    {
        return new Transformation(tag);
    }

    public static Transformation Transformation(int acceleration, params INestableTag[] tag)
    {
        return new Transformation(tag, acceleration);
    }

    public static Transformation Transformation(int from, int to, params INestableTag[] tag)
    {
        return new Transformation(tag, from, to);
    }

    public static Transformation Transformation(int from, int to, int acceleration, params INestableTag[] tag)
    {
        return new Transformation(tag, acceleration, from, to);
    }

    public static UnderLine UnderLine(bool value)
    {
        return new UnderLine(value);
    }

    public class Modded : ModdedTags;
}