
namespace Enfolder;

public interface IPipeline
{
    Task<PipelineRunResult> RunAsync(PipelineRunContext? pipelineRunContext = null, CancellationToken cancellationToken = default);

    IServiceProvider? ServiceProvider { get; set; }
}
