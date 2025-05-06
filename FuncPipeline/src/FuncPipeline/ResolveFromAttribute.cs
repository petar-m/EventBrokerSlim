using System.Text;

namespace FuncPipeline;

/// <summary>
/// Specifies how a parameter should be resolved during pipeline execution.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class ResolveFromAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the primary source from which the parameter should be resolved. Default is <see cref="Source.Services"/>.
    /// </summary>
    public Source PrimarySource { get; init; } = Source.Services;

    /// <summary>
    /// Gets or sets a value indicating whether to fall back to a secondary source if the primary source fails. default is true.
    /// </summary>
    public bool Fallback { get; init; } = true;

    /// <summary>
    /// Gets or sets the behavior when the parameter is not found in the primary source. Default is <see cref="NotFoundBehavior.ReturnTypeDefault"/>.
    /// </summary>
    /// <remarks>
    /// If <see cref="Fallback"/> is set to true, this behavior will be ignored if the parameter is not found in the primary source.
    /// </remarks>
    public NotFoundBehavior PrimaryNotFound { get; init; } = NotFoundBehavior.ReturnTypeDefault;

    /// <summary>
    /// Gets or sets the behavior when the parameter is not found in the secondary source. Default is <see cref="NotFoundBehavior.ReturnTypeDefault"/>.
    /// </summary>
    public NotFoundBehavior SecondaryNotFound { get; init; } = NotFoundBehavior.ReturnTypeDefault;

    /// <summary>
    /// Gets or sets an optional key to use for resolving the parameter, when the service is registered as keyed.
    /// </summary>
    /// <remarks>
    /// This is only applicable when resolving service from <see cref="IPipeline.ServiceProvider"/>.
    /// </remarks>
    public string? Key { get; init; } = null;

    /// <summary>
    /// Gets the default instance of the <see cref="ResolveFromAttribute"/> class.
    /// </summary>
    /// <remarks>
    /// Default values are: <see cref="PrimarySource"/> = <see cref="Source.Services"/>, <see cref="Fallback"/> = true, <see cref="PrimaryNotFound"/> = <see cref="NotFoundBehavior.ReturnTypeDefault"/>, <see cref="SecondaryNotFound"/> = <see cref="NotFoundBehavior.ReturnTypeDefault"/>.
    /// </remarks> 
    public static ResolveFromAttribute Default = new ResolveFromAttribute();

    /// <summary>
    /// Returns a string representation of the <see cref="ResolveFromAttribute"/>.
    /// </summary>
    /// <returns>A string describing the attribute's properties.</returns>
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

/// <summary>
/// Specifies the source from which a parameter should be resolved.
/// </summary>
public enum Source
{
    /// <summary>
    /// Resolve the parameter from the Service Provider.
    /// </summary>
    Services,

    /// <summary>
    /// Resolve the parameter from the Pipeline Context.
    /// </summary>
    Context
}

/// <summary>
/// Specifies the behavior when a parameter is not found in the specified source.
/// </summary>
public enum NotFoundBehavior
{
    /// <summary>
    /// Throw an exception if the parameter is not found.
    /// </summary>
    ThrowException,

    /// <summary>
    /// Return the default value of the parameter's type if not found.
    /// </summary>
    ReturnTypeDefault
}
