using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Enfolder;

public class PipelineRegistry : IPipelineRegistry
{
    private readonly FrozenDictionary<string, ImmutableArray<IPipeline>> _pipelines;

    public PipelineRegistry(IEnumerable<IPipeline> pipelines, IServiceProvider? serviceProvider = null)
    {
        _pipelines = pipelines
            .Select(x =>
            {
                x.ServiceProvider ??= serviceProvider;
                return x;
            })
            .GroupBy(x => x.Key)
            .ToFrozenDictionary(
                x => x.Key,
                x => x.Select(y => y).ToImmutableArray());
    }

    public ImmutableArray<IPipeline> Get(string key)
    {
        if(_pipelines.TryGetValue(key, out ImmutableArray<IPipeline> pipelines))
        {
            return pipelines;
        }

        return ImmutableArray<IPipeline>.Empty;
    }
}
