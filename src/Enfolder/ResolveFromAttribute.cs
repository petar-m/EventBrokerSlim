namespace Enfolder;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class ResolveFromAttribute : Attribute
{
    public Source PrimarySource { get; init; } = Source.Services;
    
    public bool Fallback { get; init; } = true;
    
    public NotFoundBehavior PrimaryNotFound { get; init;  } = NotFoundBehavior.ReturnTypeDefault;
    
    public NotFoundBehavior SecondaryNotFound { get; init; } = NotFoundBehavior.ReturnTypeDefault;
    
    public string? Key { get; init; } = null;

    public static ResolveFromAttribute Default = new ResolveFromAttribute();
}

public enum Source
{
    Services,
    Context
}

public enum NotFoundBehavior
{
    ThrowException,
    ReturnTypeDefault
}
