namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Transformation : Tag
{
    public override string Name => "t";

    public INestableTag[] Tags { get; }

    public float Acceleration { get; } = 1;

    public int From { get; }

    public int To { get; }

    public Transformation(INestableTag[] tags)
    {
        Tags = tags;
    }

    public Transformation(INestableTag[] tags, int acceleration)
    {
        Tags = tags;
        Acceleration = acceleration;
    }

    public Transformation(INestableTag[] tags, int from, int to)
    {
        Tags = tags;
        From = from;
        To = to;
    }

    public Transformation(INestableTag[] tags, int acceleration, int from, int to)
    {
        Tags = tags;
        Acceleration = acceleration;
        From = from;
        To = to;
    }


    public override string ToString()
    {
        var tags = string.Join("", Tags.Select(tag => tag.ToString()));
        var accel = Math.Abs(Acceleration - 1) < float.MinValue ? "" : $"{Acceleration},";
        var time = From == 0 && To == 0 ? "" : $"{From},{To},";
        return $"\\{Name}({time}{accel}{tags})";
    }
}