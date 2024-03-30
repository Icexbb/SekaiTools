namespace SekaiToolsCore.SubStationAlpha.Tag.Modded;

public abstract class BoundariesDeformingBase(int arg) : Tag, INestableTag
{
    public int Arg { get; set; } = arg;

    public abstract override string Name { get; }
    public override string ToString() => $"\\{Name}{Arg}";
}

public class BoundariesDeforming(int arg) : BoundariesDeformingBase(arg)
{
    public override string Name => "rnd";
}

public class BoundariesDeformingX(int arg) : BoundariesDeformingBase(arg)
{
    public override string Name => "rndx";
}

public class BoundariesDeformingY(int arg) : BoundariesDeformingBase(arg)
{
    public override string Name => "rndy";
}

public class BoundariesDeformingZ(int arg) : BoundariesDeformingBase(arg)
{
    public override string Name => "rndz";
}