using Microsoft.Maui.Storage;

namespace OlcuAtolyesi.Services;

/// <summary>Android/iOS/Windows üzerinde MAUI'nin yerel Preferences deposunu kullanır.</summary>
public sealed class MauiPreferenceStore : IPreferenceStore
{
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Preferences.Default.ContainsKey(key)
            ? Preferences.Default.Get(key, string.Empty)
            : null;
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        Preferences.Default.Set(key, value);
    }
}
