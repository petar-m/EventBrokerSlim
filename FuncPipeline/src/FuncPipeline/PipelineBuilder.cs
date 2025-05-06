using FuncPipeline.Internal;

namespace FuncPipeline;

public class PipelineBuilder
{
    private readonly List<Pipeline> _pipelines = new();

    internal PipelineBuilder(IServiceProvider? serviceProvider = null)
    {
        ServiceProvider = serviceProvider;
    }

    public static PipelineBuilder Create(IServiceProvider? serviceProvider = null) => new PipelineBuilder(serviceProvider);

    public IReadOnlyList<IPipeline> Pipelines => _pipelines;

    public IServiceProvider? ServiceProvider { get; }

    public ExecuteFunc NewPipeline() => new ExecuteFunc(this);

    private void AddPipeline(Pipeline pipeline) => _pipelines.Add(pipeline);

    public class ExecuteFunc
    {
        private readonly PipelineBuilder _pipelineBuilder;
        private readonly List<FunctionObject> _functions = new();

        internal protected ExecuteFunc(PipelineBuilder pipelineBuilder)
        {
            _pipelineBuilder = pipelineBuilder;
        }

        public WrapFunc Execute(Func<Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1>(Func<T1, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2>(Func<T1, T2, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3>(Func<T1, T2, T3, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return new WrapFunc(_pipelineBuilder, _functions);
        }
    }

    public class WrapFunc
    {
        private readonly PipelineBuilder _pipelineBuilder;
        private readonly List<FunctionObject> _functions;

        internal WrapFunc(PipelineBuilder pipelineBuilder, List<FunctionObject> functions)
        {
            _pipelineBuilder = pipelineBuilder;
            _functions = functions;
        }

        public PipelineBuilder Build(Action<IPipeline>? onBuild = null)
        {
            var pipeline = new Pipeline(_functions)
            {
                ServiceProvider = _pipelineBuilder.ServiceProvider
            };
            _pipelineBuilder.AddPipeline(pipeline);
            onBuild?.Invoke(pipeline);
            return _pipelineBuilder;
        }

        public WrapFunc WrapWith(Func<Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1>(Func<T1, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }
        public WrapFunc WrapWith<T1, T2>(Func<T1, T2, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3>(Func<T1, T2, T3, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> function, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        {
            _functions.Add(FunctionObject.Create(function, parameterAttribute));
            return this;
        }
    }
}
