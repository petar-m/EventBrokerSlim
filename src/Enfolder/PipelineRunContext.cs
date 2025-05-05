namespace Enfolder;

public class PipelineRunContext
{
    private readonly Dictionary<Type, object> _contextItems = new();

    internal bool TryGet(Type type, out object? value) => _contextItems.TryGetValue(type, out value);

    public bool TryGet<T>(out T? value)
    {
        if(_contextItems.TryGetValue(typeof(T), out object? item))
        {
            value = (T)item;
            return true;
        }

        value = default;
        return false;
    }

    public PipelineRunContext Set(Type itemType, object contextItem)
    {
        _contextItems[itemType] = contextItem;
        return this;
    }

    public PipelineRunContext Set<T>(object contextItem)
    {
        _contextItems[typeof(T)] = contextItem;
        return this;
    }

    public PipelineRunContext Remove(Type itemType)
    {
        _contextItems.Remove(itemType);
        return this;
    }

    public void Clear() => _contextItems.Clear();
}
