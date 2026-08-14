namespace SekaiToolsMedia;

internal sealed class BoundedLineBuffer
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly Queue<string> _lines = new();

    public BoundedLineBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_lock)
        {
            _lines.Enqueue(line.Trim());
            while (_lines.Count > _capacity)
                _lines.Dequeue();
        }
    }

    public void Clear()
    {
        lock (_lock) _lines.Clear();
    }

    public override string ToString()
    {
        lock (_lock) return string.Join(Environment.NewLine, _lines);
    }
}
