using System.Collections.Immutable;

namespace Enfolder;

public interface IPipelineRegistry
{
    ImmutableArray<IPipeline> Get(string key);
}
