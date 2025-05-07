namespace FuncPipeline;

public class PipelineRunOptions
{
    public bool ServiceScopePerFunction { get; init; } = true;

    public static PipelineRunOptions Default = new PipelineRunOptions();
}
