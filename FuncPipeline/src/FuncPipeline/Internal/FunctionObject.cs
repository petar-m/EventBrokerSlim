using System.Reflection;

namespace FuncPipeline.Internal;

internal sealed class FunctionObject
{
    internal record Parameter(Type Type, object? DefaultValue, ResolveFromAttribute ResolveFrom)
    {
        public static Parameter From<T>(ResolveFromAttribute resolveFrom) => new Parameter(typeof(T), default(T), resolveFrom);
    };

    private FunctionObject(object function, Parameter[] parameters)
    {
        Function = function;
        Parameters = parameters;
    }

    internal object Function { get; }

    internal Parameter[] Parameters { get; }

    internal async Task ExecuteAsync(object?[] parameterValues)
    {
        ArgumentNullException.ThrowIfNull(parameterValues, nameof(parameterValues));

        if(parameterValues.Length < Parameters.Length)
        {
            throw new ArgumentException(nameof(parameterValues), $"Expected parameters count: {Parameters.Length}, actual was: {parameterValues.Length}");
        }

        var call = Parameters.Length switch
        {
            0 => ((Func<Task>)Function)(),
            1 => ((Func<object?, Task>)Function)(parameterValues[0]),
            2 => ((Func<object?, object?, Task>)Function)(parameterValues[0], parameterValues[1]),
            3 => ((Func<object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2]),
            4 => ((Func<object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3]),
            5 => ((Func<object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4]),
            6 => ((Func<object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5]),
            7 => ((Func<object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6]),
            8 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7]),
            9 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8]),
            10 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9]),
            11 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10]),
            12 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10], parameterValues[11]),
            13 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10], parameterValues[11], parameterValues[12]),
            14 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10], parameterValues[11], parameterValues[12], parameterValues[13]),
            15 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10], parameterValues[11], parameterValues[12], parameterValues[13], parameterValues[14]),
            16 => ((Func<object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, object?, Task>)Function)(parameterValues[0], parameterValues[1], parameterValues[2], parameterValues[3], parameterValues[4], parameterValues[5], parameterValues[6], parameterValues[7], parameterValues[8], parameterValues[9], parameterValues[10], parameterValues[11], parameterValues[12], parameterValues[13], parameterValues[14], parameterValues[15]),
            _ => throw new InvalidOperationException("Can't execute Func with more than 16 arguments."),
        };

        await call.ConfigureAwait(false);
    }

    internal static FunctionObject Create(Func<Task> func)
       => new FunctionObject(func, Array.Empty<Parameter>());

    internal static FunctionObject Create<T1>(Func<T1, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0])
        };

        return new FunctionObject(
            (object o1) => func((T1)o1),
            parameters);
    }

    internal static FunctionObject Create<T1, T2>(Func<T1, T2, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1])
        };
        return new FunctionObject(
            (object o1, object o2) => func((T1)o1, (T2)o2),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3>(Func<T1, T2, T3, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2])
        };

        return new FunctionObject(
            (object o1, object o2, object o3) => func((T1)o1, (T2)o2, (T3)o3),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10]),
            Parameter.From<T12>(parameterAttributes[11])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11, object o12) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11, (T12)o12),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10]),
            Parameter.From<T12>(parameterAttributes[11]),
            Parameter.From<T13>(parameterAttributes[12])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11, object o12, object o13) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11, (T12)o12, (T13)o13),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10]),
            Parameter.From<T12>(parameterAttributes[11]),
            Parameter.From<T13>(parameterAttributes[12]),
            Parameter.From<T14>(parameterAttributes[13])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11, object o12, object o13, object o14) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11, (T12)o12, (T13)o13, (T14)o14),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10]),
            Parameter.From<T12>(parameterAttributes[11]),
            Parameter.From<T13>(parameterAttributes[12]),
            Parameter.From<T14>(parameterAttributes[13]),
            Parameter.From<T15>(parameterAttributes[14])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11, object o12, object o13, object o14, object o15) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11, (T12)o12, (T13)o13, (T14)o14, (T15)o15),
            parameters);
    }

    internal static FunctionObject Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));
        ResolveFromAttribute[] parameterAttributes = GetParameterAttributes(func.Method, parameterAttribute);
        var parameters = new[]
        {
            Parameter.From<T1>(parameterAttributes[0]),
            Parameter.From<T2>(parameterAttributes[1]),
            Parameter.From<T3>(parameterAttributes[2]),
            Parameter.From<T4>(parameterAttributes[3]),
            Parameter.From<T5>(parameterAttributes[4]),
            Parameter.From<T6>(parameterAttributes[5]),
            Parameter.From<T7>(parameterAttributes[6]),
            Parameter.From<T8>(parameterAttributes[7]),
            Parameter.From<T9>(parameterAttributes[8]),
            Parameter.From<T10>(parameterAttributes[9]),
            Parameter.From<T11>(parameterAttributes[10]),
            Parameter.From<T12>(parameterAttributes[11]),
            Parameter.From<T13>(parameterAttributes[12]),
            Parameter.From<T14>(parameterAttributes[13]),
            Parameter.From<T15>(parameterAttributes[14]),
            Parameter.From<T16>(parameterAttributes[15])
        };

        return new FunctionObject(
            (object o1, object o2, object o3, object o4, object o5, object o6, object o7, object o8, object o9, object o10, object o11, object o12, object o13, object o14, object o15, object o16) => func((T1)o1, (T2)o2, (T3)o3, (T4)o4, (T5)o5, (T6)o6, (T7)o7, (T8)o8, (T9)o9, (T10)o10, (T11)o11, (T12)o12, (T13)o13, (T14)o14, (T15)o15, (T16)o16),
            parameters);
    }

    private static ResolveFromAttribute[] GetParameterAttributes(MethodInfo func, Dictionary<int, ResolveFromAttribute>? parameterAttribute = null)
        => func.GetParameters()
            .Select(x => parameterAttribute?.GetValueOrDefault(x.Position)
                         ?? x.GetCustomAttributes(typeof(ResolveFromAttribute), false).SingleOrDefault()
                         ?? ResolveFromAttribute.Default)
            .Cast<ResolveFromAttribute>()
            .ToArray();
}
