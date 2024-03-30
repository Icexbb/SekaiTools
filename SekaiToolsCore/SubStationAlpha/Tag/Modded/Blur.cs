namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public abstract class BlurBase : Tag, INestableTag
{
    public abstract override string Name { get; }
    public float Strength { get; }

    protected BlurBase(float strength = 0)
    {
        if (strength < 0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be greater than or equal to 0.");
        Strength = strength;
    }

    public override string ToString() => $"\\{Name}{Strength}";
}

public class XBlur(float strength = 0) : BlurBase(strength)
{
    public override string Name => "xblur";
}

public class YBlur(float strength = 0) : BlurBase(strength)
{
    public override string Name => "yblur";
}