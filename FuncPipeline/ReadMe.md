# FuncPipeline

FuncPipeline is a library for building and executing function pipelines leveraging nested function composition. It supports dependency injection, custom parameter resolution, and asynchronous execution.

## Building Pipelines

Pipelines are created with a fluent API using the `PipelineBuilder` class.  
Each `Execute` call defines a function in the pipeline.  
Functions will be executed in the order of definition. `INext.RunAsync()` defines the call to the next function in the pipeline.

*Example:*
```csharp
var pipelineBuilder = pipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (INext next) =>
    {
        Console.WriteLine("Before A");
        await next.RunAsync();
        Console.WriteLine("After A");
    })
    .Execute(async () => Console.WriteLine("A"))
    .Build();

// When run, this pipeline will produce the output:
// Before A
// A
// After A
```

`PipelineBuilder` can create multiple pipelines by using `NewPipeline()` after `Build()`.   
`PipelineBuilder.Pipelines` exposes created pipelines in the order of their definition.  
`PipelineBuilder.Build(Action<IPipeline>? onBuild = null)` accepts an optional callback invoked with the currently built pipeline.

## Executing Pipelines  

Once a pipeline is built, it is executed with the `RunAsync` method of the `IPipeline` interface. The pipeline optionally accepts a `PipelineRunContext` and a `CancellationToken`.  

*Example:*
```csharp
PipelineRunResult result = await pipeline.RunAsync();
```

`IPipeline.RunAsync()` does not throw exceptions.  
`PipelineRunResult.IsSuccessful` indicates whether the pipeline completed without exceptions. If there was an exception, it is accessible through `PipelineRunResult.Exception`.  

`PipelineRunResult.Context` exposes the `PipelineRunContext` instance used in the run.

## Parameter Resolution  

FuncPipeline supports function parameter resolution from:  
- **IServiceProvider**: Using the service provider either passed in `PipelineBuilder.Create(IServiceScopeFactory? serviceScopeFactory = null)` or set to the `IPipeline.ServiceScopeFactory` property.  
Function dependencies are always resolved from a new `IServiceScope`, disposed of after each run. Functions can share a scope, or alternatively, each function uses a separate scope. This is controlled on a pipeline level by passing `PipelineRunOptions` to `PipelineBuilder.NewPipeline(PipelineRunOptions? options = null)`.  
The default is `PipelineRunOptions.ServiceScopePerFunction = true`.
- **PipelineRunContext**: Using the `PipelineRunContext` instance passed to `IPipeline.RunAsync()`.

Parameters always available:
- `INext` instance for invoking the next function.
- `PipelineRunContext` - the one passed to `IPipeline.RunAsync()`, or an internally created instance for the run. 
- `CancellationToken` - the one passed to `IPipeline.RunAsync()` or `default`.

*Example:*
```csharp
var pipelineBuilder = pipelineBuilder.Create()
    .NewPipeline()
    // Parameters always available without any setup
    .Execute(async (INext next, PipelineRunContext context, CancellationToken ct) =>
    {
        context.Set<int>(123);
        await next.RunAsync();
    })
    .Execute(async () => Console.WriteLine("A"))
    .Build();
```

The default behavior of parameter resolution follows the order:
- Try to resolve from `IServiceProvider`.
- Try to resolve from `PipelineRunContext`.
- Return the default value for the parameter type.

Each pipeline run has a `PipelineRunContext`. It is passed as a parameter to `IPipeline.RunAsync()`, or created internally.  
`PipelineRunContext` is a wrapper over `Dictionary<Type, object>` and can be used to pass parameters that are not services registered in the service provider. It can be used to pass data between functions and is available through `PipelineRunResult.Context` when the pipeline run is completed.  
Values are stored in the context based on their type. This allows them to be injected as parameters as if they were registered in the service provider.  

*Example:*
```csharp
serviceCollection.Add<IService, Service>();
...
var pipelineBuilder = pipelineBuilder.Create(serviceProvider)
    .NewPipeline()
    .Execute(async (
        INext next /* always available */, 
        PipelineRunContext context /* always available */,
        IService service /* resolved from serviceProvider */) 
        =>
        {
            context.Set<int>(123); 
            await next.RunAsync();
        })    
    .Execute(async (
        IContextService contextService /* resolved from context */,
        int integer /* resolved from context */ )  
        => 
        {
            ...
        })
    .Build();
...
var context = new PipelineRunContext()
   .Set<IContextService>(instance);

var result = await pipeline.RunAsync(context);
_ = result.TryGet<int>(out var integer); // integer = 123
```

## Customizing Parameter Resolution Behavior

The `ResolveFromAttribute` allows control over how parameters are resolved. 
- **PrimarySource**: The primary source for resolution. `Source.Services` (default) or `Source.Context`.
- **Fallback**: Whether to fall back to a secondary source if the primary source fails. Default is `true`.
- **PrimaryNotFound**: Behavior when the parameter is not found in the primary source. `NotFoundBehavior.ThrowException` or `NotFoundBehavior.ReturnTypeDefault` (default).
- **SecondaryNotFound**: Behavior when the parameter is not found in the secondary source.  `NotFoundBehavior.ThrowException` or `NotFoundBehavior.ReturnTypeDefault` (default).
- **Key**: An optional key for resolving the parameter, for keyed `IServiceProvider` registrations.

`ResolveFromAttribute` can decorate a function parameter or alternatively can be passed as a parameter when building the pipeline.

*Example:*
```csharp
pipelineBuilder
    .NewPipeline()
    // Change parameter resolution behavior
    .Execute(static async (
        [ResolveFrom(PrimarySource = Source.Services, Fallback = true, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service key")] 
        IService1 s1,
        [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)] 
        IService1 s2) => { ... })
     // Equivalent behavior definition    
    .Execute(static async (
        IService1 s1,
        IService1 s2) => { ... },
        new Dictionary<int, ResolveFromAttribute>
        {
            { 
                0, // Should match the parameter position (s1)
                new ResolveFromAttribute
                {
                    PrimarySource = Source.Services,
                    Fallback = true,
                    SecondaryNotFound = NotFoundBehavior.ThrowException,
                    Key = "service key"
                }
            },
            { 
                1, // Should match the parameter position (s2)
                new ResolveFromAttribute
                {
                    PrimarySource = Source.Context,
                    Fallback = true,
                    PrimaryNotFound = NotFoundBehavior.ThrowException
                }
            }            
        })
    .Build();
```
