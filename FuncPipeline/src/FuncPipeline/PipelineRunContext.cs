namespace FuncPipeline;

/// <summary>
/// Represents the context for a pipeline run, containing data shared across pipeline functions.
/// </summary>
public class PipelineRunContext
{
    private readonly Dictionary<Type, object> _contextItems = new();

    /// <summary>
    /// Tries to retrieve a context item of the specified type.
    /// </summary>
    /// <param name="type">The type of the context item to retrieve.</param>
    /// <param name="value">The retrieved context item, or null if not found.</param>
    /// <returns>True if the context item was found; otherwise, false.</returns>
    internal bool TryGet(Type type, out object? value) => _contextItems.TryGetValue(type, out value);

    /// <summary>
    /// Tries to retrieve a context item of the specified generic type.
    /// </summary>
    /// <typeparam name="T">The type of the context item to retrieve.</typeparam>
    /// <param name="value">The retrieved context item, or default if not found.</param>
    /// <returns>True if the context item was found; otherwise, false.</returns>
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

    /// <summary>
    /// Sets a context item of the specified type. If the item is already set, it will be replaced.
    /// </summary>
    /// <param name="itemType">The type of the context item to set.</param>
    /// <param name="contextItem">The context item to set.</param>
    /// <returns>The current <see cref="PipelineRunContext"/> instance.</returns>
    public PipelineRunContext Set(Type itemType, object contextItem)
    {
        _contextItems[itemType] = contextItem;
        return this;
    }

    /// <summary>
    /// Sets a context item of the specified generic type. If the item is already set, it will be replaced.
    /// </summary>
    /// <typeparam name="T">The type of the context item to set.</typeparam>
    /// <param name="contextItem">The context item to set.</param>
    /// <returns>The current <see cref="PipelineRunContext"/> instance.</returns>
    public PipelineRunContext Set<T>(object contextItem)
    {
        _contextItems[typeof(T)] = contextItem;
        return this;
    }

    /// <summary>
    /// Removes a context item of the specified type.
    /// </summary>
    /// <param name="itemType">The type of the context item to remove.</param>
    /// <returns>The current <see cref="PipelineRunContext"/> instance.</returns>
    public PipelineRunContext Remove(Type itemType)
    {
        _contextItems.Remove(itemType);
        return this;
    }

    /// <summary>
    /// Clears all context items from the pipeline run context.
    /// </summary>
    public void Clear() => _contextItems.Clear();
}
