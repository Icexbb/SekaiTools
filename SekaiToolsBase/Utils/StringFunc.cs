namespace SekaiToolsBase.Utils;

public static class StringFunc
{
    public static int LineCount(this string str)
    {
        return str.Split('\n').Select(value => value.Length > 0 ? 1 : 0).Sum();
    }

    public static int Count(this string str, string part)
    {
        var count = 0;
        var i = 0;
        while ((i = str.IndexOf(part, i, StringComparison.Ordinal)) != -1)
        {
            i += part.Length;
            count++;
        }

        return count;
    }

    public static string TrimAll(this string str)
    {
        return str.Trim().Replace("\n", "")
            .Replace("\\R", "")
            .Replace("\\N", "");
    }

    public static string EscapedReturn(this string str)
    {
        return str.Replace("\\N", "\n")
            .Replace("\\R", "\n");
    }

    public static int MaxLineLength(this string str)
    {
        return str.Split('\n').Max(x => x.Trim().Length);
    }
}