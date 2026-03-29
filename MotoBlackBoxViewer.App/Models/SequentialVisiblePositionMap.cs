using System.Collections;

namespace MotoBlackBoxViewer.App.Models;

internal sealed class SequentialVisiblePositionMap : IReadOnlyDictionary<int, int>
{
    private readonly int _firstIndex;
    private readonly int _count;

    public SequentialVisiblePositionMap(int firstIndex, int count)
    {
        _firstIndex = firstIndex;
        _count = count;
    }

    public int this[int key]
        => TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException($"The given point index '{key}' was not present in the visible range.");

    public IEnumerable<int> Keys
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return _firstIndex + i;
        }
    }

    public IEnumerable<int> Values
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return i + 1;
        }
    }

    public int Count => _count;

    public bool ContainsKey(int key)
        => key >= _firstIndex && key < _firstIndex + _count;

    public bool TryGetValue(int key, out int value)
    {
        if (ContainsKey(key))
        {
            value = key - _firstIndex + 1;
            return true;
        }

        value = 0;
        return false;
    }

    public IEnumerator<KeyValuePair<int, int>> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            int key = _firstIndex + i;
            yield return new KeyValuePair<int, int>(key, i + 1);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
