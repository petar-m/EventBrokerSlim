
namespace Enfolder;

public interface IPipeline
{
    string Key { get; }

    Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default);

    IServiceProvider? ServiceProvider { get; set; }
}
