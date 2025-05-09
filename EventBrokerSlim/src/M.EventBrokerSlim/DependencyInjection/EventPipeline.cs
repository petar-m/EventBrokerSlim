using System;
using FuncPipeline;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Represents a pipeline for handling events.
/// </summary>
/// <param name="Event">The type of the event.</param>
/// <param name="Pipeline">The pipeline to process the event.</param>
public record EventPipeline(Type Event, IPipeline Pipeline);
