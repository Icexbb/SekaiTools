using System.Globalization;

namespace SekaiToolsBase.Story.Translation;

public readonly record struct TextDifferenceRange(int Start, int Length);

public sealed record TextDifferenceResult(
    IReadOnlyList<TextDifferenceRange> Current,
    IReadOnlyList<TextDifferenceRange> Reference);

public static class TextDifference
{
    public static TextDifferenceResult Compare(string current, string reference)
    {
        if (current == reference) return new([], []);
        var left = Elements(current);
        var right = Elements(reference);
        var prefix = 0;
        while (prefix < left.Values.Length && prefix < right.Values.Length &&
               left.Values[prefix] == right.Values[prefix]) prefix++;
        var leftEnd = left.Values.Length;
        var rightEnd = right.Values.Length;
        while (leftEnd > prefix && rightEnd > prefix && left.Values[leftEnd - 1] == right.Values[rightEnd - 1])
        {
            leftEnd--;
            rightEnd--;
        }

        var currentRanges = new List<TextDifferenceRange>();
        var referenceRanges = new List<TextDifferenceRange>();
        var rows = leftEnd - prefix;
        var columns = rightEnd - prefix;
        // Subtitle lines are short. Bound work for unusually large pasted text,
        // retaining the common prefix/suffix even when the middle is too large.
        if ((long)(rows + 1) * (columns + 1) > 1_000_000)
        {
            Add(prefix, leftEnd, prefix, rightEnd);
            return new(currentRanges, referenceRanges);
        }

        var lengths = new int[rows + 1, columns + 1];
        for (var i = rows - 1; i >= 0; i--)
            for (var j = columns - 1; j >= 0; j--)
                lengths[i, j] = left.Values[prefix + i] == right.Values[prefix + j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        var x = prefix;
        var y = prefix;
        var startX = x;
        var startY = y;
        while (x < leftEnd || y < rightEnd)
        {
            if (x < leftEnd && y < rightEnd && left.Values[x] == right.Values[y])
            {
                Add(startX, x, startY, y);
                startX = ++x;
                startY = ++y;
            }
            else if (x < leftEnd && (y == rightEnd ||
                     lengths[x - prefix + 1, y - prefix] >= lengths[x - prefix, y - prefix + 1])) x++;
            else y++;
        }
        Add(startX, x, startY, y);
        return new(currentRanges, referenceRanges);

        void Add(int fromX, int toX, int fromY, int toY)
        {
            if (fromX == toX && fromY == toY) return;
            currentRanges.Add(new(left.Starts[fromX], left.Starts[toX] - left.Starts[fromX]));
            referenceRanges.Add(new(right.Starts[fromY], right.Starts[toY] - right.Starts[fromY]));
        }
    }

    private static (int[] Starts, string[] Values) Elements(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
        var values = Enumerable.Range(0, starts.Length - 1)
            .Select(index => text[starts[index]..starts[index + 1]].Replace("\r\n", "\n").Replace('\r', '\n'))
            .ToArray();
        return (starts, values);
    }
}
