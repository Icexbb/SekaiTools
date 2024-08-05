namespace SekaiToolsCore.SubStationAlpha;

public class Event : ICloneable
{
    private Event(
        string type, int layer, string start, string end, string style, string name, int marginL,
        int marginR, int marginV, string effect, string text)
    {
        Type = type;
        Layer = layer;
        Start = start;
        End = end;
        Style = style;
        Name = name;
        MarginL = marginL;
        MarginR = marginR;
        MarginV = marginV;
        Effect = effect;
        Text = text;
    }

    public int Layer { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public string Type { get; set; }
    public string Style { get; set; }
    public string Name { get; set; }
    public string Effect { get; set; }
    public string Start { get; set; }
    public string End { get; set; }
    public string Text { get; set; }

    public object Clone()
    {
        return new Event(Type, Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text);
    }

    public static Event Dialog(string text,
        string start, string end, string style = "Default", int layer = 0, string name = "",
        int marginL = 0, int marginR = 0, int marginV = 0, string effect = "")
    {
        return new Event(
            "Dialogue", layer, start, end, style, name, marginL, marginR, marginV, effect, text);
    }

    public static Event Comment(string text,
        string start, string end, string style = "Default", int layer = 0, string name = "",
        int marginL = 0, int marginR = 0, int marginV = 0, string effect = "")
    {
        return new Event(
            "Comment", layer, start, end, style, name, marginL, marginR, marginV, effect, text);
    }

    public Event ToDialog()
    {
        var res = (Event)Clone();
        res.Type = "Dialogue";
        return res;
    }

    public Event ToComment()
    {
        var res = (Event)Clone();
        res.Type = "Comment";
        return res;
    }

    public static Event FromString(string source)
    {
        if (!source.StartsWith("Dialogue:") && !source.StartsWith("Comment:"))
            throw new Exception("Source Not Start With Marker");
        var typeSplit = source.Split(':', 2);
        var sourcePart = typeSplit[1].Split(',');
        switch (sourcePart.Length)
        {
            case < 10:
                throw new Exception("Source Parameter not Enough");
            case > 10:
            {
                var text = sourcePart[9];
                for (var i = 10; i < sourcePart.Length; i++) text += $",{sourcePart[i]}";

                sourcePart[9] = text;
                break;
            }
        }

        return new Event(
            typeSplit[0].Replace(':', ' ').Trim(),
            int.TryParse(sourcePart[0], out var result) ? result : 0,
            sourcePart[1].Trim(),
            sourcePart[2].Trim(),
            sourcePart[3].Trim(),
            sourcePart[4].Trim(),
            int.TryParse(sourcePart[5], out result) ? result : 0,
            int.TryParse(sourcePart[6], out result) ? result : 0,
            int.TryParse(sourcePart[7], out result) ? result : 0,
            sourcePart[8].Trim(),
            sourcePart[9].Trim()
        );
    }

    public override string ToString()
    {
        return $"{Type}: {Layer},{Start},{End},{Style},{Name},{MarginL},{MarginR},{MarginV},{Effect},{Text}";
    }
}