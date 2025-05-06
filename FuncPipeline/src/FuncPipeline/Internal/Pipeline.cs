using System.Collections.Immutable;

namespace FuncPipeline.Internal;

internal class Pipeline : IPipeline
{
    internal Pipeline(List<FunctionObject> functions)
    {
        ArgumentNullException.ThrowIfNull(functions, nameof(functions));
        Functions = functions.ToImmutableArray();
    }

    public IServiceProvider? ServiceProvider { get; set; }

    internal ImmutableArray<FunctionObject> Functions { get; }

    public async Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default)
    {
        var pipelineRunner = new PipelineRunner(this, pipelineRunContext, ServiceProvider, cancellationToken);

        try
        {
            await pipelineRunner.RunAsync().ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            return new PipelineRunResult(ex, pipelineRunner.Context);
        }

        return new PipelineRunResult(pipelineRunner.Context);
    }
}
