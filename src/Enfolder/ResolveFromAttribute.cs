using System.Text;

namespace Enfolder;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class ResolveFromAttribute : Attribute
{
    public Source PrimarySource { get; init; } = Source.Services;

    public bool Fallback { get; init; } = true;

    public NotFoundBehavior PrimaryNotFound { get; init; } = NotFoundBehavior.ReturnTypeDefault;

    public NotFoundBehavior SecondaryNotFound { get; init; } = NotFoundBehavior.ReturnTypeDefault;

    public string? Key { get; init; } = null;

    public static ResolveFromAttribute Default = new ResolveFromAttribute();

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(nameof(ResolveFromAttribute)).Append(" { ");
        stringBuilder.Append(nameof(PrimarySource)).Append(" = ").Append(PrimarySource).Append(", ");
        stringBuilder.Append(nameof(Fallback)).Append(" = ").Append(Fallback).Append(", ");
        stringBuilder.Append(nameof(PrimaryNotFound)).Append(" = ").Append(PrimaryNotFound).Append(", ");
        stringBuilder.Append(nameof(SecondaryNotFound)).Append(" = ").Append(SecondaryNotFound).Append(", ");
        stringBuilder.Append(nameof(Key)).Append(" = ").Append(Key).Append(" }");
        return stringBuilder.ToString();
    }
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
