using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace FuncPipeline.Internal;

internal class Pipeline : IPipeline
{
    internal Pipeline(List<FunctionObject> functions, PipelineRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(functions, nameof(functions));
        Functions = functions.ToImmutableArray();
        Options = options;
    }

    public IServiceScopeFactory? ServiceScopeFactory { get; set; }
    
    public PipelineRunOptions Options { get; }

    internal ImmutableArray<FunctionObject> Functions { get; }

    public async Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default)
    {
        IServiceScope? scope = null;
        if(!Options.ServiceScopePerFunction)
        {
            scope = ServiceScopeFactory?.CreateScope();
        }

        var pipelineRunner = new PipelineRunner(this, pipelineRunContext, ServiceScopeFactory, cancellationToken, scope);

        try
        {
            await pipelineRunner.RunAsync().ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            return new PipelineRunResult(ex, pipelineRunner.Context);
        }
        finally
        {
            if(!Options.ServiceScopePerFunction)
            {
                scope?.Dispose();
            }
        }

        return new PipelineRunResult(pipelineRunner.Context);
    }
}
