namespace SekaiToolsCore.Process;

public enum FrameType
{
    Exact,
    Start,
    End
}

public class SmpteTime(int hour, int minute, int second, int frame, string separator = ":") : ICloneable
{
    public int Hour = hour;
    public int Minute = minute;
    public int Second = second;
    public int Frame = frame;
    private string _separator = separator;
    public object Clone() => new SmpteTime(Hour, Minute, Second, Frame);


    public override string ToString() =>
        $"{Hour:00}{_separator}{Minute:00}{_separator}{Second:00}{_separator}{Frame:00}";


    public static SmpteTime FromString(string str, string separator = ":")
    {
        var parts = str.Split(separator);
        if (parts.Length != 4)
            throw new FormatException("Invalid SMPTE time format");
        var hour = int.Parse(parts[0]);
        var minute = int.Parse(parts[1]);
        var second = int.Parse(parts[2]);
        var frame = int.Parse(parts[3]);
        return new SmpteTime(hour, minute, second, frame);
    }
}

public class FrameRate
{
    private const long DefaultDenominator = 1000000000L;
    private readonly long _denominator;
    private readonly long _numerator;
    private const long Last = 0;
    private readonly List<int> _timecodes = [];
    private readonly bool _drop;
    public double Fps() => _numerator / (double)_denominator;

    public FrameRate(double fps)
    {
        switch (fps)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(fps), "fps must be greater than 0");
            case >= 1000:
                throw new ArgumentOutOfRangeException(nameof(fps), "fps must be less than 1000");
        }

        _denominator = DefaultDenominator;
        _numerator = (long)(fps * DefaultDenominator);
        _timecodes.Add(0);
    }

    public FrameRate(long numerator, long denominator, bool drop)
    {
        if (numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator),
                "numerator and denominator must be greater than 0");
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "numerator and denominator must be greater than 0");
        if (numerator / denominator > 1000)
            throw new ArgumentOutOfRangeException(nameof(numerator), "fps must be less than 1000");
        _denominator = denominator;
        _numerator = numerator;
        _drop = drop && _numerator % _denominator != 0;
        _timecodes.Add(0);
    }

    // private void SetFromTimecodes(IEnumerable<int> timecodes)
    // {
    //     _timecodes = timecodes.ToList();
    //     ValidateTimecodes(_timecodes);
    //     _timecodes = NormalizeTimecodes(_timecodes);
    //     _denominator = DefaultDenominator;
    //     _numerator = (long)((_timecodes.Count - 1) * _denominator * 1000 / _timecodes.Last());
    //     _last = (_timecodes.Count - 1) * _denominator * 1000;
    //     return;
    //
    //     void ValidateTimecodes(IReadOnlyCollection<int> tc)
    //     {
    //         if (tc.Count <= 1)
    //             throw new IndexOutOfRangeException("timecodes must have at least 2 elements");
    //         if (!tc.IsSorted(true))
    //             throw new ArgumentException("timecodes must be sorted");
    //         if (tc.First() == tc.Last())
    //             throw new ArgumentException("timecodes must have different elements");
    //     }
    //
    //     List<int> NormalizeTimecodes(List<int> tc)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }

    // public FrameRate(IEnumerable<int> timecodes)
    // {
    //     SetFromTimecodes(timecodes);
    // }

    // public FrameRate(string path)
    // {
    //     throw new NotImplementedException();
    //     if (!File.Exists(path))
    //         throw new FileNotFoundException("file not found", path);
    //     _denominator = DefaultDenominator;
    //     var lines = File.ReadAllLines(path);
    //     switch (lines.First())
    //     {
    //         case "# timecode format v2":
    //             SetFromTimecodes(lines.Skip(1).Select(int.Parse).ToList());
    //             break;
    //         case "# timecode format v1" when lines[1].StartsWith("Assume "):
    //             V1Parse(lines.Skip(1).ToList());
    //             break;
    //         default:
    //             throw new InvalidDataException("invalid file format");
    //     }
    //
    //     return;
    //
    //     void V1Parse(IEnumerable<string> _)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }

    // public void Save(string path)
    // {
    //     var lines = new List<string> { "# timecode format v2" };
    //     for (var i = 0; i < _timecodes.Count; i++) lines.Add(TimeAtFrame(i).ToString());
    //     File.WriteAllLines(path, lines);
    // }

    public int FrameAtTime(int ms, FrameType type = FrameType.Exact)
    {
        if (type == FrameType.Start) return FrameAtTime(ms - 1) + 1;
        if (type == FrameType.End) return FrameAtTime(ms - 1);

        if (ms < 0) return (int)((ms * _numerator / _denominator - 999) / 1000);
        if (ms > _timecodes.Last())
            return (int)((ms * _numerator - Last + _denominator - 1) / _denominator / 1000) +
                   (_timecodes.Count - 1);
        return _timecodes.Select((t, i) => (t, i)).First(p => p.t >= ms).i;
    }

    public int FrameAtSmpte(SmpteTime smpte)
    {
        var ifps = (int)double.Ceiling(Fps());
        var st = (SmpteTime)smpte.Clone();
        if (_drop && _denominator == 1001 && _numerator % 30000 == 0)
        {
            var dropFactor = (int)(_numerator / 30000);
            var oneMinute = 60 * 30 * dropFactor - dropFactor * 2;
            var tenMinutes = 60 * 10 * 30 * dropFactor - dropFactor * 18;

            var tenM = st.Minute / 10;
            st.Minute %= 10;

            if (st.Minute != 0 && st.Second == 0 && st.Frame < 2 * dropFactor) st.Frame = 2 * dropFactor;

            return st.Hour * tenMinutes * 6 + tenM * tenMinutes + st.Minute * oneMinute + st.Second * ifps + st.Frame;
        }

        if (_drop && Math.Abs(ifps - Fps()) > double.MinValue)
        {
            var frame = (st.Hour * 60 * 60 + st.Minute * 60 + st.Second) * ifps + st.Frame;
            return (int)(frame / (double)ifps * Fps() + 0.5);
        }

        return (st.Hour * 60 * 60 + st.Minute * 60 + st.Second) * ifps + st.Frame;
    }

    public SubtitleTime TimeAtFrame(int frame, FrameType type = FrameType.Exact)
    {
        switch (type)
        {
            case FrameType.Start:
            {
                var prev = TimeAtFrame(frame - 1).Milliseconds;
                var cur = TimeAtFrame(frame).Milliseconds;
                return new SubtitleTime(prev + (cur - prev + 1) / 2);
            }
            case FrameType.End:
            {
                var cur = TimeAtFrame(frame).Milliseconds;
                var next = TimeAtFrame(frame + 1).Milliseconds;
                return new SubtitleTime(cur + (next - cur + 1) / 2);
            }
            case FrameType.Exact:
            {
                if (frame < 0) return new SubtitleTime((int)(frame * _denominator * 1000 / _numerator));

                if (frame < _timecodes.Count) return new SubtitleTime(_timecodes[frame]);

                var framesPastEnd = frame - _timecodes.Count + 1;
                return new SubtitleTime((int)(
                    (framesPastEnd * 1000 * _denominator + Last + _numerator / 2) / _numerator));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public SubtitleTime TimeAtSmpte(SmpteTime smpte)
    {
        return TimeAtFrame(FrameAtSmpte(smpte));
    }

    public SmpteTime SmpteAtFrame(int frame)
    {
        frame = int.Max(frame, 0);
        var ifps = (int)double.Ceiling(Fps());
        if (_drop && _denominator == 1001 && _numerator % 30000 == 0)
        {
            var dropFactor = (int)(_numerator / 30000);
            var oneMinute = 60 * 30 * dropFactor - dropFactor * 2;
            var tenMinutes = 60 * 10 * 30 * dropFactor - dropFactor * 18;
            var tenMinuteGroups = frame / tenMinutes;
            var lastTenMinutes = frame % tenMinutes;

            frame += tenMinuteGroups * 18 * dropFactor;
            frame += (lastTenMinutes - 2 * dropFactor) / oneMinute * 2 * dropFactor;
        }
        else if (_drop && Math.Abs(ifps - Fps()) > double.MinValue)
        {
            frame = (int)(frame / Fps() * ifps + 0.5);
        }

        return new SmpteTime(frame / (ifps * 60 * 60),
            frame / (ifps * 60) % 60,
            frame / ifps % 60,
            frame % ifps
        );
    }

    public SmpteTime SmpteAtTime(int ms)
    {
        return SmpteAtFrame(FrameAtTime(ms));
    }


    public bool IsVfr() => _timecodes.Count > 1;
    public bool IsLoaded() => _numerator > 0;
    public bool NeedDropFrames() => _drop;

    public static FrameRate Fps_23_976 { get; } = new(24000, 1001, true);

    public static FrameRate Fps_24 { get; } = new(24);

    public static FrameRate Fps_25 { get; } = new(25);

    public static FrameRate Fps_29_97 { get; } = new(30000, 1001, true);

    public static FrameRate Fps_30 { get; } = new(30);

    public static FrameRate Fps_50 { get; } = new(50);

    public static FrameRate Fps_59_94 { get; } = new(60000, 1001, true);

    public static FrameRate Fps_60 { get; } = new(60);
}

public class SubtitleTime(int ms = 0)
{
    private const int MaxTime = 10 * 60 * 60 * 1000 - 6;
    private const int MinTime = 0;
    public int Milliseconds { get; } = Utils.Middle(MinTime, ms, MaxTime);

    public SubtitleTime(string text) : this()
    {
        var afterDecimal = -1;
        var current = 0;
        foreach (var c in text)
        {
            switch (c)
            {
                case ':':
                    Milliseconds = Milliseconds * 60 + current;
                    current = 0;
                    break;
                case '.' or ',':
                    Milliseconds = (Milliseconds * 60 + current) * 1000;
                    current = 0;
                    afterDecimal = 100;
                    break;
                case < '0' or > '9':
                    continue;
                default:
                {
                    if (afterDecimal < 0)
                    {
                        current *= 10;
                        current += c - '0';
                    }
                    else
                    {
                        Milliseconds += (c - '0') * afterDecimal;
                        afterDecimal /= 10;
                    }

                    break;
                }
            }
        }

        if (afterDecimal < 0) Milliseconds = (Milliseconds * 60 + current) * 1000;

        Milliseconds = Utils.Middle(0, Milliseconds, 10 * 60 * 60 * 1000 - 6);
    }

    private int ToInt() => Milliseconds + 5 - (Milliseconds + 5) % 10;

    public static explicit operator int(SubtitleTime time) => time.ToInt();
    public static SubtitleTime operator +(SubtitleTime a) => a;
    public static SubtitleTime operator -(SubtitleTime a) => new(-a.Milliseconds);

    public static SubtitleTime operator +(SubtitleTime a, SubtitleTime b) => new(a.Milliseconds + b.Milliseconds);
    public static SubtitleTime operator -(SubtitleTime a, SubtitleTime b) => new(a.Milliseconds - b.Milliseconds);

    public string GetAssFormatted(bool msPrecision = false)
    {
        var assTime = msPrecision ? Milliseconds : ToInt();
        var hour = assTime / 3600000;
        var minute = assTime / 60000 % 60;
        var second = assTime / 1000 % 60;
        var ms = assTime % 1000;

        return $"{hour:0}:{minute:00}:{second:00}." + (msPrecision ? $"{ms:000}" : $"{ms / 10:00}");
    }

    public string GetSrtFormatted()
    {
        var assTime = ToInt();
        var hour = assTime / 3600000;
        var minute = assTime / 60000 % 60;
        var second = assTime / 1000 % 60;
        var ms = assTime % 1000;
        return $"{hour:00}:{minute:00}:{second:00},{ms:000}";
    }
}