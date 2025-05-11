using System.Threading.Tasks;

namespace FuncPipeline.Tests;

public interface IMock
{
    Task Do();
    Task Do(IArg1 arg1);
    Task Do(IArg1 arg1, IArg2 arg2);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13, IArg14 arg14);
    Task Do(IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13, IArg14 arg14, IArg15 arg15);
}
