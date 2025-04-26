namespace Enfolder;

public class PipelineRunContext
{
    private readonly Dictionary<Type, object> _contextItems = new();

    internal bool TryGet(Type type, out object? value) => _contextItems.TryGetValue(type, out value);

    public T? Get<T>() => _contextItems.TryGetValue(typeof(T), out object? value) ? (T)value : default;

    public PipelineRunContext Set<T>(T contextItem) where T : notnull
    {
        _contextItems[typeof(T)] = contextItem;
        return this;
    }

    public PipelineRunContext Remove<T>()
    {
        _contextItems.Remove(typeof(T));
        return this;
    }

    public void Clear() => _contextItems.Clear();
}
