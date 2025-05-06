namespace FuncPipeline;

/// <summary>
/// Represents the result of a pipeline run, including its success status and context.
/// </summary>
public class PipelineRunResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunResult"/> class with an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred during the pipeline run.</param>
    /// <param name="context">The context of the pipeline run.</param>
    public PipelineRunResult(Exception exception, PipelineRunContext context)
    {
        Exception = exception;
        Context = context;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunResult"/> class for a successful run.
    /// </summary>
    /// <param name="context">The context of the pipeline run.</param>
    public PipelineRunResult(PipelineRunContext context)
    {
        IsSuccessful = true;
        Context = context;
    }

    /// <summary>
    /// Gets a value indicating whether the pipeline run was successful.
    /// </summary>
    public bool IsSuccessful { get; }

    /// <summary>
    /// Gets the exception that occurred during the pipeline run, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the context of the pipeline run.
    /// </summary>
    public PipelineRunContext Context { get; }
}
