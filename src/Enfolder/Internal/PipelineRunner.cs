using System.Buffers;
using Microsoft.Extensions.DependencyInjection;

namespace Enfolder.Internal;

internal class PipelineRunner : INext
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly Pipeline _pipeline;
    private readonly CancellationToken _cancellationToken;

    private int _current;

    internal PipelineRunner(Pipeline pipeline, PipelineRunContext? context, IServiceProvider? serviceProvider, CancellationToken cancellationToken = default)
    {
        _pipeline = pipeline;
        Context = context ?? new PipelineRunContext();
        _serviceProvider = serviceProvider;
        _cancellationToken = cancellationToken;
        _current = pipeline.Functions.Length;
    }

    internal PipelineRunContext Context { get; }

    public async Task RunAsync()
    {
        _current--;
        if(_current < 0)
        {
            return;
        }

        FunctionObject function = _pipeline.Functions[_current];
        using IServiceScope? scope = _serviceProvider?.CreateScope();
        object?[] parameterValues = ArrayPool<object?>.Shared.Rent(function.Parameters.Length);
        try
        {
            for(int i = 0; i < function.Parameters.Length; i++)
            {
                if(function.Parameters[i].Type == typeof(INext))
                {
                    parameterValues[i] = this;
                }
                else if(function.Parameters[i].Type == typeof(CancellationToken))
                {
                    parameterValues[i] = _cancellationToken;
                }
                else if(function.Parameters[i].Type == typeof(PipelineRunContext))
                {
                    parameterValues[i] = Context;
                }
                else
                {
                    object? value = null;
                    if(scope != null)
                    {
                        value = scope.ServiceProvider.GetService(function.Parameters[i].Type);
                    }

                    if(value is null && !Context.TryGet(function.Parameters[i].Type, out value))
                    {
                        value = function.Parameters[i].DefaultValue;
                    }

                    parameterValues[i] = value;
                   
                }
            }

            await function.ExecuteAsync(parameterValues).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<object?>.Shared.Return(parameterValues);
        }
    }
}
