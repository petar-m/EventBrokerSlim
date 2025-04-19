namespace Enfolder;

public class PipelineRunResult
{
    public PipelineRunResult(Exception exception, PipelineRunContext context)
    {
        Exception = exception;
        Context = context;
    }

    public PipelineRunResult(PipelineRunContext context)
    {
        IsSuccessful = true;
        Context = context;
    }

    public bool IsSuccessful { get; }

    public Exception? Exception { get; }

    public PipelineRunContext Context { get; }
}
