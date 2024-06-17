using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal static class DelegateHelper
{
    public static DelegateHandlerDescriptor BuildDelegateHandlerDescriptor(Delegate delegateHandler, Type eventType)
    {
        if(delegateHandler.Method.ReturnType != typeof(Task))
        {
            throw new ArgumentException("Delegate must return Task.");
        }

        ParameterInfo[] delegateParameters = delegateHandler.Method.GetParameters();
        if(delegateParameters.Length > 16)
        {
            throw new ArgumentException("Delegate can't have more than 16 arguments.");
        }

        return new()
        {
            EventType = eventType,
            ParamTypes = delegateParameters.Select(x => x.ParameterType).ToArray(),
            Handler = CompileDelegateToFunc(delegateHandler, delegateParameters)
        };
    }

    private static object CompileDelegateToFunc(Delegate delegateHandler, ParameterInfo[] delegateParameters)
    {
        List<ParameterExpression> parametersAsObject = new();
        List<Expression> parametersCastToOriginalType = new();
        foreach(ParameterInfo parameterInfo in delegateParameters)
        {
            parametersAsObject.Add(Expression.Parameter(typeof(object)));
            parametersCastToOriginalType.Add(Expression.Convert(parametersAsObject.Last(), parameterInfo.ParameterType));
        }

        var call = Expression.Call(
            instance: delegateHandler.Target is null ? null : Expression.Constant(delegateHandler.Target),
            method: delegateHandler.Method,
            arguments: parametersCastToOriginalType);

        return delegateParameters.Length switch
        {
            0 => Expression.Lambda<Func<Task>>(call)
                           .Compile(),
            1 => Expression.Lambda<Func<object, Task>>(call, parametersAsObject[0])
                           .Compile(),
            2 => Expression.Lambda<Func<object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            3 => Expression.Lambda<Func<object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            4 => Expression.Lambda<Func<object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            5 => Expression.Lambda<Func<object, object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            6 => Expression.Lambda<Func<object, object, object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            7 => Expression.Lambda<Func<object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            8 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            9 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                           .Compile(),
            10 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            11 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            12 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            13 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            14 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            15 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            16 => Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>>(call, parametersAsObject)
                            .Compile(),
            _ => throw new InvalidOperationException("Can't convert Delegate with more than 16 arguments to Func."),
        };
    }

    public static async Task ExecuteDelegateHandler(object handler, object[] parameters, int parametersCount)
    {
        var call = parametersCount switch
        {
            0 => ((Func<Task>)handler)(),
            1 => ((Func<object, Task>)handler)(parameters[0]),
            2 => ((Func<object, object, Task>)handler)(parameters[0], parameters[1]),
            3 => ((Func<object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2]),
            4 => ((Func<object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3]),
            5 => ((Func<object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4]),
            6 => ((Func<object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5]),
            7 => ((Func<object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6]),
            8 => ((Func<object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7]),
            9 => ((Func<object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8]),
            10 => ((Func<object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9]),
            11 => ((Func<object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10]),
            12 => ((Func<object, object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10], parameters[11]),
            13 => ((Func<object, object, object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10], parameters[11], parameters[12]),
            14 => ((Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10], parameters[11], parameters[12], parameters[13]),
            15 => ((Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10], parameters[11], parameters[12], parameters[13], parameters[14]),
            16 => ((Func<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, Task>)handler)(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6], parameters[7], parameters[8], parameters[9], parameters[10], parameters[11], parameters[12], parameters[13], parameters[14], parameters[15]),
            _ => throw new InvalidOperationException("Can't execute Func with more than 16 arguments."),
        };

        await call;
    }
}
