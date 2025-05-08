using Microsoft.Extensions.DependencyInjection;

namespace FuncPipeline;

/// <summary>
/// Represents a pipeline that can execute a series of functions in a defined order.
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// Executes the pipeline with the provided context and cancellation token.
    /// </summary>
    /// <param name="pipelineRunContext">The context for the pipeline run, containing necessary data for execution.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="PipelineRunResult"/> containing the result of the pipeline execution.</returns>
    Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or sets the service scope factory used to creates scopes for resolving dependencies during pipeline execution.
    /// </summary>
    IServiceScopeFactory? ServiceScopeFactory { get; set; }
}
