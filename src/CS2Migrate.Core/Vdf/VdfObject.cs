namespace CS2Migrate.Core.Vdf;

public sealed class VdfObject
{
    private readonly Dictionary<string, List<VdfValue>> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Add(string key, VdfValue value)
    {
        if (!_entries.TryGetValue(key, out var values))
        {
            values = [];
            _entries.Add(key, values);
        }

        values.Add(value);
    }

    public bool TryGetString(string key, out string value)
    {
        value = _entries.TryGetValue(key, out var values)
            ? values.LastOrDefault(item => item.Scalar is not null)?.Scalar ?? string.Empty
            : string.Empty;
        return value.Length > 0;
    }

    public bool TryGetObject(string key, out VdfObject value)
    {
        value = _entries.TryGetValue(key, out var values)
            ? values.LastOrDefault(item => item.Object is not null)?.Object ?? new VdfObject()
            : new VdfObject();
        return _entries.ContainsKey(key) && values?.Any(item => item.Object is not null) == true;
    }

    public IEnumerable<KeyValuePair<string, VdfObject>> Objects()
    {
        foreach (var entry in _entries)
        {
            foreach (var value in entry.Value.Where(item => item.Object is not null))
            {
                yield return new KeyValuePair<string, VdfObject>(entry.Key, value.Object!);
            }
        }
    }
}

internal sealed record VdfValue(string? Scalar, VdfObject? Object)
{
    public static VdfValue FromScalar(string value) => new(value, null);
    public static VdfValue FromObject(VdfObject value) => new(null, value);
}
