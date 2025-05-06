using System;
using FuncPipeline;

namespace M.EventBrokerSlim.DependencyInjection;

public record EventPipeline(Type Event, IPipeline Pipeline);
