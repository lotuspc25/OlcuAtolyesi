namespace OlcuAtolyesi.Services;

/// <summary>Testler ve önizlemeler için işlem belleğinde tercih deposu.</summary>
public sealed class MemoryPreferenceStore : IPreferenceStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.GetValueOrDefault(key);
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _values[key] = value;
    }
}
