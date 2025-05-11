namespace FuncPipeline;

/// <summary>
/// Represents the next step in a pipeline, allowing for continuation of execution.
/// </summary>
public interface INext
{
    /// <summary>
    /// Executes the next step in the pipeline asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RunAsync();
}
