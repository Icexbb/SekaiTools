namespace SekaiToolsCore.SubStationAlpha.Tag;

public class Bold : Tag
{
    public Bold(int weight)
    {
        switch (weight)
        {
            case 0 or 1:
                Weight = weight;
                break;
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be greater than or equal to 0.");
            case > 1000:
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be less than or equal to 1000.");
            default:
            {
                if (weight % 100 != 0)
                    throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be a multiple of 100.");
                Weight = weight;
                break;
            }
        }
    }

    public override string Name => "b";
    public int Weight { get; }

    public override string ToString() => $"\\{Name}{Weight}";
}