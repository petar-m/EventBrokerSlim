using System.Threading;
using System.Threading.Tasks;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Represents a pipeline implementation that performs no operations and always returns a successful result.
/// </summary>
/// <remarks>Use this class to register event handler for publish-only scenarios.</remarks>
public class NullPipeline : IPipeline
{
    private static readonly PipelineRunResult _successResult = new PipelineRunResult(new PipelineRunContext());

    /// <summary>
    /// A singleton instance of the <see cref="NullPipeline"/> class, which can be used wherever an <see cref="IPipeline"/> implementation is required but no actual processing is needed. This instance will always return a successful result when its <see cref="RunAsync"/> method is called, making it suitable for scenarios where event handlers are registered for publish-only purposes without any processing logic.
    /// </summary>
    public static NullPipeline Instance { get; } = new NullPipeline();

    private NullPipeline()
    {
    }

    /// <summary>
    /// Always returns null, as the <see cref="NullPipeline"/> does not require any services to run. This property is implemented to satisfy the <see cref="IPipeline"/> interface but is effectively a no-op in this implementation, since the pipeline does not perform any operations or require any dependencies.
    /// </summary>
    public IServiceScopeFactory? ServiceScopeFactory { get => null; set { } }

    /// <summary>
    /// This method simulates the execution of a pipeline by immediately returning a successful result encapsulated in a <see cref="PipelineRunResult"/> object. The method accepts an optional <see cref="PipelineRunContext"/> parameter and a <see cref="CancellationToken"/>, but these parameters are not utilized in the logic of this implementation, as the method simply returns a predefined successful result without performing any processing or checks. This makes the <see cref="NullPipeline"/> suitable for scenarios where event handlers are registered for publish-only purposes without any processing logic, allowing the system to function without requiring an actual pipeline execution.
    /// </summary>
    /// <param name="pipelineRunContext">No-op parameter, not used in this implementation.</param>
    /// <param name="cancellationToken">No-op parameter, not used in this implementation.</param>
    /// <returns>A task that represents the asynchronous operation, containing a successful <see cref="PipelineRunResult"/>.</returns>
    public Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default) => Task.FromResult(_successResult);
}
