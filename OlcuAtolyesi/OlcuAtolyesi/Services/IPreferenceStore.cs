namespace OlcuAtolyesi.Services;

public interface IPreferenceStore
{
    string? Get(string key);

    void Set(string key, string value);
}
