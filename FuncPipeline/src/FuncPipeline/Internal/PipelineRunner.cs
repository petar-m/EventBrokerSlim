using System.Buffers;
using Microsoft.Extensions.DependencyInjection;

namespace FuncPipeline.Internal;

internal class PipelineRunner : INext
{
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly Pipeline _pipeline;
    private readonly CancellationToken _cancellationToken;
    private readonly IServiceScope? _scope;
    private int _current;

    internal PipelineRunner(Pipeline pipeline, PipelineRunContext? context, IServiceScopeFactory? serviceScopeFactory, CancellationToken cancellationToken = default, IServiceScope? scope = null)
    {
        _pipeline = pipeline;
        Context = context ?? new PipelineRunContext();
        _serviceScopeFactory = serviceScopeFactory;
        _cancellationToken = cancellationToken;
        _scope = scope;
        _current = -1;
    }

    internal PipelineRunContext Context { get; }

    public async Task RunAsync()
    {
        _current++;
        if(_current >= _pipeline.Functions.Length)
        {
            return;
        }

        FunctionObject function = _pipeline.Functions[_current];
        IServiceScope? scope = _pipeline.Options.ServiceScopePerFunction
            ? _serviceScopeFactory?.CreateScope()
            : _scope;
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
                    parameterValues[i] = Resolve(function.Parameters[i], scope);
                }
            }

            await function.ExecuteAsync(parameterValues).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<object?>.Shared.Return(parameterValues, true);
            if(_pipeline.Options.ServiceScopePerFunction)
            {
                scope?.Dispose();
            }
        }
    }

    private object? Resolve(FunctionObject.Parameter parameter, IServiceScope? scope)
    {
        ResolveFromAttribute resolveFromAttribute = parameter.ResolveFrom;
        switch(resolveFromAttribute.PrimarySource)
        {
            case Source.Services:
                {
                    if(!resolveFromAttribute.Fallback)
                    {
                        return GetFromServices(parameter, resolveFromAttribute.PrimaryNotFound, scope);
                    }

                    object? value = GetService(parameter, scope);

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

            case Source.Context:
                {
                    if(Context.TryGet(parameter.Type, out object? value))
                    {
                        return value;
                    }

                    if(!resolveFromAttribute.Fallback)
                    {
                        return GetDefaultOrThrow(parameter, resolveFromAttribute.PrimaryNotFound);
                    }

                    return GetFromServices(parameter, resolveFromAttribute.SecondaryNotFound, scope);
                }

            default:
                throw new ArgumentException($"{nameof(Source)} enum value {resolveFromAttribute.PrimarySource} is not supported. {parameter.ResolveFrom}.");
        }
    }

    private static object? GetDefaultOrThrow(FunctionObject.Parameter parameter, NotFoundBehavior notFoundBehavior)
        => notFoundBehavior switch
        {
            NotFoundBehavior.ReturnTypeDefault => parameter.DefaultValue,
            NotFoundBehavior.ThrowException => throw new ArgumentException($"No {parameter.Type.FullName} found in {nameof(PipelineRunContext)}. {parameter.ResolveFrom}"),
            _ => throw new ArgumentException($"{nameof(NotFoundBehavior)} enum value {notFoundBehavior} is not supported. {parameter.ResolveFrom}"),
        };

    private object? GetFromServices(FunctionObject.Parameter parameter, NotFoundBehavior notFoundBehavior, IServiceScope? scope)
        => notFoundBehavior switch
        {
            NotFoundBehavior.ThrowException => GetRequiredService(parameter, scope),
            NotFoundBehavior.ReturnTypeDefault => GetService(parameter, scope),
            _ => throw new ArgumentException($"{nameof(NotFoundBehavior)} enum value {notFoundBehavior} is not supported. {parameter.ResolveFrom}"),
        };

    private object? GetRequiredService(FunctionObject.Parameter parameter, IServiceScope? scope)
    {
        if(scope is null)
        {
            throw new ArgumentException($"IPipeline.ServiceProvider is null. Cannot resolve parameter of type {parameter.Type.FullName}. {parameter.ResolveFrom}");
        }

        try
        {
            return parameter.ResolveFrom.Key is null
                ? scope.ServiceProvider.GetRequiredService(parameter.Type)
                : scope.ServiceProvider.GetRequiredKeyedService(parameter.Type, parameter.ResolveFrom.Key);
        }
        catch(InvalidOperationException ex)
        {
            var withKey = parameter.ResolveFrom.Key is null ? string.Empty : $" with key {parameter.ResolveFrom.Key}";
            throw new ArgumentException($"No service for type {parameter.Type.FullName} has been registered{withKey}. {parameter.ResolveFrom}.", ex);
        }
    }

    private object? GetService(FunctionObject.Parameter parameter, IServiceScope? scope)
    {
        return parameter.ResolveFrom.Key is null
            ? scope?.ServiceProvider.GetService(parameter.Type)
            : (scope?.ServiceProvider as IKeyedServiceProvider)?.GetKeyedService(parameter.Type, parameter.ResolveFrom.Key);
    }
}
