namespace Enfolder;

public class KeyFromTypeResolver : IPipelineKeyResolver
{
    private readonly string _key;

    public KeyFromTypeResolver(Type type)
    {
        ArgumentNullException.ThrowIfNull(type.FullName, nameof(type.FullName));
        _key = type.FullName;
    }

    public string Key() => _key;
}
