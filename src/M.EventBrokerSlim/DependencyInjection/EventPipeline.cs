using System;
using Enfolder;

namespace M.EventBrokerSlim.DependencyInjection;

public record EventPipeline(Type Event, IPipeline Pipeline);
