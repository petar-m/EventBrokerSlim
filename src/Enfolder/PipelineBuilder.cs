using Enfolder.Internal;

namespace Enfolder;

public class PipelineBuilder
{
    private readonly List<Pipeline> _pipelines = new();

    public PipelineBuilder()
    {
    }

    public PipelineBuilder(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IReadOnlyList<IPipeline> Pipelines => _pipelines;
    
    public IServiceProvider? ServiceProvider { get; }
    
    public ExecuteFunc For(IPipelineKeyResolver pipelineResolver) => new ExecuteFunc(this, pipelineResolver);

    public ExecuteFunc For(string key) => new ExecuteFunc(this, new KeyFromStringResolver(key));

    public ExecuteFunc For<T>() => new ExecuteFunc(this, new KeyFromTypeResolver(typeof(T)));

    private void AddPipeline(Pipeline pipeline) => _pipelines.Add(pipeline);

    public class ExecuteFunc
    {
        private readonly PipelineBuilder _pipelineBuilder;
        private readonly IPipelineKeyResolver _pipelineKeyResolver;
        private readonly List<FunctionObject> _functions = new();

        internal protected ExecuteFunc(PipelineBuilder pipelineBuilder, IPipelineKeyResolver pipelineKeyResolver)
        {
            _pipelineBuilder = pipelineBuilder;
            _pipelineKeyResolver = pipelineKeyResolver;
        }

        public WrapFunc Execute(Func<Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1>(Func<T1, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }
        public WrapFunc Execute<T1, T2>(Func<T1, T2, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3>(Func<T1, T2, T3, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }

        public WrapFunc Execute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return new WrapFunc(_pipelineBuilder, _pipelineKeyResolver, _functions);
        }
    }

    public class WrapFunc
    {
        private readonly PipelineBuilder _pipelineBuilder;
        private readonly IPipelineKeyResolver _pipelineKeyResolver;
        private readonly List<FunctionObject> _functions;

        internal WrapFunc(PipelineBuilder pipelineBuilder, IPipelineKeyResolver pipelineKeyResolver, List<FunctionObject> functions)
        {
            _pipelineBuilder = pipelineBuilder;
            _pipelineKeyResolver = pipelineKeyResolver;
            _functions = functions;
        }

        public PipelineBuilder Build()
        {
            var pipeline = new Pipeline(_pipelineKeyResolver.Key(), _functions)
            {
                ServiceProvider = _pipelineBuilder.ServiceProvider
            };

            _pipelineBuilder.AddPipeline(pipeline);
            return _pipelineBuilder;
        }

        public WrapFunc WrapWith(Func<Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1>(Func<T1, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }
        public WrapFunc WrapWith<T1, T2>(Func<T1, T2, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3>(Func<T1, T2, T3, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }

        public WrapFunc WrapWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> function)
        {
            _functions.Add(FunctionObject.Create(function));
            return this;
        }
    }
}
