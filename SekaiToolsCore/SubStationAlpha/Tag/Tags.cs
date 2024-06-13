using System.Drawing;
using SekaiToolsCore.SubStationAlpha.Tag.Modded;

namespace SekaiToolsCore.SubStationAlpha.Tag;

public partial class Tags(params Tag[] tags)
{
    private readonly Tag[] _tags = tags;
    public override string ToString() => "{" + string.Join("", _tags.Select(tag => tag.ToString())) + "}";

    public static implicit operator Tags(Tag[] tags) => new(tags);

    public static Tags FromITag(Tag tag) => new(tag);

    public static Tags operator +(Tags tags, Tag tag) => new(tags._tags.Append(tag).ToArray());

    public static Tags operator +(Tag tag, Tags tags) => new(tags._tags.Prepend(tag).ToArray());

    public static Tags operator +(Tags tags1, Tags tags2) => new(tags1._tags.Concat(tags2._tags).ToArray());

    public static string operator +(Tags tags, string text) => tags.ToString() + text;

    public static string operator +(string text, Tags tags) => text + tags.ToString();
}

public partial class Tags
{
    public static Alpha Alpha(int a) => new(a);
    public static Alpha Alpha(SubStationAlpha.Alpha a) => new(a);

    public static PrimaryAlpha PrimaryAlpha(int a) => new(a);
    public static PrimaryAlpha PrimaryAlpha(SubStationAlpha.Alpha a) => new(a);

    public static SecondaryAlpha SecondaryAlpha(int a) => new(a);
    public static SecondaryAlpha SecondaryAlpha(SubStationAlpha.Alpha a) => new(a);

    public static OutlineAlpha OutlineAlpha(int a) => new(a);
    public static OutlineAlpha OutlineAlpha(SubStationAlpha.Alpha a) => new(a);

    public static ShadowAlpha ShadowAlpha(int a) => new(a);
    public static ShadowAlpha ShadowAlpha(SubStationAlpha.Alpha a) => new(a);

    public static Anchor Anchor(AnchorPos pos) => new(pos);
    public static Anchor Anchor(int pos) => new(pos);

    public static AnchorTraditional AnchorTraditional(AnchorTraditionalPos pos) => new(pos);
    public static AnchorTraditional AnchorTraditional(int pos) => new(pos);

    public static Be Be(int value) => new(value);

    public static Blur Blur(int value) => new(value);

    public static Bold Bold(int value) => new(value);

    public static Clip Clip(int x1, int y1, int x2, int y2) => new(x1, y1, x2, y2);
    public static Clip Clip(Point from, Point to) => new(from, to);
    public static Clip Clip(Rectangle rect) => new(rect);

    public static ClipInverse ClipInverse(int x1, int y1, int x2, int y2) => new(x1, y1, x2, y2);
    public static ClipInverse ClipInverse(Point from, Point to) => new(from, to);
    public static ClipInverse ClipInverse(Rectangle rect) => new(rect);

    public static ClipVector ClipVector(AssDraw.AssDraw ad, int scale = 1) => new(ad, scale);

    public static ClipInverseVector ClipInverseVector(AssDraw.AssDraw ad, int scale = 1) => new(ad, scale);

    public static Color Color(SubStationAlpha.Color color) => new(color);
    public static Color Color(int r, int g, int b) => new(r, g, b);

    public static PrimaryColor PrimaryColor(SubStationAlpha.Color color) => new(color);
    public static PrimaryColor PrimaryColor(int r, int g, int b) => new(r, g, b);

    public static SecondaryColor SecondaryColor(SubStationAlpha.Color color) => new(color);
    public static SecondaryColor SecondaryColor(int r, int g, int b) => new(r, g, b);

    public static OutlineColor OutlineColor(SubStationAlpha.Color color) => new(color);
    public static OutlineColor OutlineColor(int r, int g, int b) => new(r, g, b);

    public static ShadowColor ShadowColor(SubStationAlpha.Color color) => new(color);
    public static ShadowColor ShadowColor(int r, int g, int b) => new(r, g, b);

    public static Fade Fade(int start, int end) => new(start, end);

    public static FadeComplex FadeComplex(int alpha1, int alpha2, int alpha3, int time1, int time2, int time3,
        int time4) => new(alpha1, alpha2, alpha3, time1, time2, time3, time4);

    public static FontEncoding FontEncoding(int type) => new(type);

    public static FontName FontName(string name) => new(name);

    public static FontRotate FontRotate(int angle) => new(angle);

    public static FontRotateX FontRotateX(int angle) => new(angle);

    public static FontRotateY FontRotateY(int angle) => new(angle);

    public static FontRotateZ FontRotateZ(int angle) => new(angle);

    public static FontScaleX FontScaleX(int value) => new(value);

    public static FontScaleY FontScaleY(int value) => new(value);

    public static FontShearingX FontShearingX(int value) => new(value);

    public static FontShearingY FontShearingY(int value) => new(value);

    public static FontSize FontSize(int value) => new(value);

    public static FontSpacing FontSpacing(int value) => new(value);

    public static Italic Italic(bool value) => new(value);

    public static Karaoke Karaoke(int duration) => new(duration);

    public static KaraokeFade KaraokeFade(int duration) => new(duration);

    public static KaraokeOutline KaraokeOutline(int duration) => new(duration);

    public static LineBreak LineBreak(LineBreakStyle style) => new(style);

    public static Move Move(int x1, int y1, int x2, int y2, int t1 = 0, int t2 = 0) => new(x1, y1, x2, y2, t1, t2);
    public static Move Move(Point from, Point to, int t1 = 0, int t2 = 0) => new(from, to, t1, t2);

    public static Paint Paint(int type) => new(type);

    public static Position Position(int x, int y) => new(x, y);
    public static Position Position(Point point) => new(point);

    public static Reset Reset(string style = "") => new(style);

    public static RotateCenter RotateCenter(int x, int y) => new(x, y);
    public static RotateCenter RotateCenter(Point point) => new(point);
    public static Bord Bord(int value) => new(value);

    public static XBord XBord(int value) => new(value);

    public static YBord YBord(int value) => new(value);
    public static Shad Shad(int value) => new(value);

    public static XShad XShad(int value) => new(value);

    public static YShad YShad(int value) => new(value);

    public static StrikeOut StrikeOut(bool value) => new(value);

    public static Transformation Transformation(params INestableTag[] tag) => new(tag);
    public static Transformation Transformation(int acceleration, params INestableTag[] tag) => new(tag, acceleration);
    public static Transformation Transformation(int from, int to, params INestableTag[] tag) => new(tag, from, to);

    public static Transformation Transformation(int from, int to, int acceleration, params INestableTag[] tag) =>
        new(tag, acceleration, from, to);

    public static UnderLine UnderLine(bool value) => new(value);

    public class Modded : ModdedTags;
}