namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAllTestTypes(this IServiceCollection serviceCollection) =>
        serviceCollection.AddTransient<A1>()
                         .AddTransient<A2>()
                         .AddTransient<A3>()
                         .AddTransient<A4>()
                         .AddTransient<A5>()
                         .AddTransient<A6>()
                         .AddTransient<A7>()
                         .AddTransient<A8>()
                         .AddTransient<A9>()
                         .AddTransient<A10>()
                         .AddTransient<A11>()
                         .AddTransient<A12>()
                         .AddTransient<A13>()
                         .AddTransient<A14>()
                         .AddTransient<A15>()
                         .AddTransient<A16>()
                         .AddTransient<A17>();
}

public record A1();
public record A2();
public record A3();
public record A4();
public record A5();
public record A6();
public record A7();
public record A8();
public record A9();
public record A10();
public record A11();
public record A12();
public record A13();
public record A14();
public record A15();
public record A16();
public record A17();

