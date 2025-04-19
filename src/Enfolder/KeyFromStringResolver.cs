namespace Enfolder;

public class KeyFromStringResolver : IPipelineKeyResolver
{
    private readonly string _key;

    public KeyFromStringResolver(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        _key = key;
    }

    public string Key() => _key;
}
