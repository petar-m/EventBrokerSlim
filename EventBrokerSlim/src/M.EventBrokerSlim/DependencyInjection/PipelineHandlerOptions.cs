namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Options for configuring pipeline-based event handler registrations
/// (<see cref="ServiceCollectionExtensions.AddEventHandlerPipeline{TEvent}(Microsoft.Extensions.DependencyInjection.IServiceCollection, FuncPipeline.IPipeline, System.Action{PipelineHandlerOptions})"/>).
/// </summary>
public class PipelineHandlerOptions : HandlerOptionsBase<PipelineHandlerOptions>
{
    internal PipelineHandlerOptions() { }
}
