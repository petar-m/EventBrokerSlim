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
                    parameterValues[i] = Resolve(function.Parameters[i]);

                }
            }

            await function.ExecuteAsync(parameterValues).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<object?>.Shared.Return(parameterValues);
        }
    }

    private object? Resolve(FunctionObject.Parameter parameter)
    {
        ResolveFromAttribute resolveFromAttribute = parameter.ResolveFrom;
        if(resolveFromAttribute.PrimarySource == Source.Services)
        {
            if(!resolveFromAttribute.Fallback)
            {
                return GetFromServices(parameter, resolveFromAttribute.PrimaryNotFound);
            }

            object? value = GetService(parameter);

            if(value is not null)
            {
                return value;
            }

            if(Context.TryGet(parameter.Type, out value))
            {
                return value;
            }

            return GetDefaultOrThrow(parameter, resolveFromAttribute.SecondaryNotFound);
        }

        if(resolveFromAttribute.PrimarySource == Source.Context)
        {
            if(Context.TryGet(parameter.Type, out object? value))
            {
                return value;
            }

            if(!resolveFromAttribute.Fallback)
            {
                return GetDefaultOrThrow(parameter, resolveFromAttribute.PrimaryNotFound);
            }

            return GetFromServices(parameter, resolveFromAttribute.SecondaryNotFound);
        }

        throw new ArgumentException($"ResolveFrom value {resolveFromAttribute.PrimarySource} is not supported. {parameter}.");
    }

    private static object? GetDefaultOrThrow(FunctionObject.Parameter parameter, NotFoundBehavior notFoundBehavior)
        => notFoundBehavior switch
        {
            NotFoundBehavior.ReturnTypeDefault => parameter.DefaultValue,
            NotFoundBehavior.ThrowException => throw new ArgumentException($"Service provider is null. Cannot resolve {parameter.Type.Name}."),// TODO: fix exception type and message
            _ => throw new ArgumentException($"{nameof(NotFoundBehavior)} value {notFoundBehavior} is not supported. {parameter}"),
        };

    private object? GetFromServices(FunctionObject.Parameter parameter, NotFoundBehavior notFoundBehavior)
        => notFoundBehavior switch
        {
            NotFoundBehavior.ThrowException => GetRequiredService(parameter),
            NotFoundBehavior.ReturnTypeDefault => GetService(parameter),
            _ => throw new ArgumentException($"{nameof(NotFoundBehavior)} value {notFoundBehavior} is not supported. {parameter}"),
        };

    private object? GetRequiredService(FunctionObject.Parameter parameter)
    {
        if(_serviceProvider is null)
        {
            throw new InvalidOperationException($"ServiceProvider is null. Cannot resolve {parameter}.");
        }

        return parameter.ResolveFrom.Key is null
            ? _serviceProvider.GetRequiredService(parameter.Type)
            : _serviceProvider.GetRequiredKeyedService(parameter.Type, parameter.ResolveFrom.Key);
    }

    private object? GetService(FunctionObject.Parameter parameter)
    {
        return parameter.ResolveFrom.Key is null
            ? _serviceProvider?.GetService(parameter.Type)
            : (_serviceProvider as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ResolveFrom.Key);
    }
}
