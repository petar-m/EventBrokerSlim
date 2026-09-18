using FuncPipeline.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace FuncPipeline;

/// <summary>
/// Represents a builder for creating and configuring pipelines.
/// </summary>
public class PipelineBuilder
{
    private readonly PipelineRunOptions _pipelineRunOptions = PipelineRunOptions.Default;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly List<FunctionObject> _functions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBuilder"/> class.
    /// </summary>
    public PipelineBuilder()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBuilder"/> class with the specified pipeline run options.
    /// </summary>
    /// <param name="options">The <see cref="PipelineRunOptions"/> instance to configure the pipeline run behavior.</param>
    public PipelineBuilder(PipelineRunOptions options)
    {
        _pipelineRunOptions = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBuilder"/> class with the specified service scope factory.
    /// </summary>
    /// <param name="serviceScopeFactory">The <see cref="IServiceScopeFactory"/> instance to use to create scopes for dependency resolution.</param>
    public PipelineBuilder(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBuilder"/> class with the specified pipeline run options and service scope factory.
    /// </summary>
    /// <param name="options">The <see cref="PipelineRunOptions"/> instance to configure the pipeline run behavior.</param>
    /// <param name="serviceScopeFactory">The <see cref="IServiceScopeFactory"/> instance to use to create scopes for dependency resolution.</param>    
    public PipelineBuilder(PipelineRunOptions options, IServiceScopeFactory serviceScopeFactory)
    {
        _pipelineRunOptions = options;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Builds the pipeline and optionally invokes a callback.
    /// </summary>
    /// <returns>The built <see cref="IPipeline"/> instance.</returns>
    public IPipeline Build()
        => new Pipeline(_functions, _pipelineRunOptions)
        {
            ServiceScopeFactory = _serviceScopeFactory
        };

    /// <summary>
    /// Adds an execution function with no parameters to the pipeline.
    /// </summary>
    /// <param name="function">The function to execute.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute(Func<Task> function)
    {
        _functions.Add(FunctionObject.Create(function));
        return this;
    }

    /// <summary>
    /// Adds an execution function with one parameter to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1>(Func<T1, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with two parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2>(Func<T1, T2, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with three parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3>(Func<T1, T2, T3, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with four parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with five parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with six parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with seven parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with eight parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with nine parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with ten parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with eleven parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with twelve parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <typeparam name="T12">The type of the twelfth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with thirteen parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <typeparam name="T12">The type of the twelfth parameter.</typeparam>
    /// <typeparam name="T13">The type of the thirteenth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with fourteen parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <typeparam name="T12">The type of the twelfth parameter.</typeparam>
    /// <typeparam name="T13">The type of the thirteenth parameter.</typeparam>
    /// <typeparam name="T14">The type of the fourteenth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with fifteen parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <typeparam name="T12">The type of the twelfth parameter.</typeparam>
    /// <typeparam name="T13">The type of the thirteenth parameter.</typeparam>
    /// <typeparam name="T14">The type of the fourteenth parameter.</typeparam>
    /// <typeparam name="T15">The type of the fifteenth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }

    /// <summary>
    /// Adds an execution function with sixteen parameters to the pipeline.
    /// </summary>
    /// <typeparam name="T1">The type of the first parameter.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="T6">The type of the sixth parameter.</typeparam>
    /// <typeparam name="T7">The type of the seventh parameter.</typeparam>
    /// <typeparam name="T8">The type of the eighth parameter.</typeparam>
    /// <typeparam name="T9">The type of the ninth parameter.</typeparam>
    /// <typeparam name="T10">The type of the tenth parameter.</typeparam>
    /// <typeparam name="T11">The type of the eleventh parameter.</typeparam>
    /// <typeparam name="T12">The type of the twelfth parameter.</typeparam>
    /// <typeparam name="T13">The type of the thirteenth parameter.</typeparam>
    /// <typeparam name="T14">The type of the fourteenth parameter.</typeparam>
    /// <typeparam name="T15">The type of the fifteenth parameter.</typeparam>
    /// <typeparam name="T16">The type of the sixteenth parameter.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="parameterAttribute">Optional attributes for parameter resolution.</param>
    /// <returns>A <see cref="PipelineBuilder"/> instance to define next function or complete the pipeline.</returns>
    public PipelineBuilder Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        _functions.Add(FunctionObject.Create(function, parameterAttribute));
        return this;
    }
}
