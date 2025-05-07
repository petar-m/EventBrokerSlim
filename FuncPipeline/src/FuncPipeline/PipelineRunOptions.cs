namespace FuncPipeline;

/// <summary>
/// Represents the options for running a pipeline.
/// </summary>
public class PipelineRunOptions
{
    /// <summary>
    /// Gets a value indicating whether a new service scope is created for each function in the pipeline.
    /// </summary>
    public bool ServiceScopePerFunction { get; init; } = true;

    /// <summary>
    /// Gets the default pipeline run options.
    /// </summary>
    public static PipelineRunOptions Default = new PipelineRunOptions();
}
