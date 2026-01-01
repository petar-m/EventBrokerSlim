namespace M.EventBrokerSlim.Tests;

public class EventNameRegistryTests
{
    private readonly EventNameRegistry _eventNameRegistry = new();

    [Fact]
    public void Add_allows_chaining()
    {
        var returned = _eventNameRegistry.Add<TestEvent>("MyEvent");

        Assert.Same(_eventNameRegistry, returned);
    }

    [Fact]
    public void Add_registers_event_with_given_name()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent");

        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent"));
    }

    [Fact]
    public void Add_registering_event_twice_is_a_no_op()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent").Add<TestEvent>("MyEvent");

        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent"));
    }

    [Fact]
    public void Add_registering_event_twice_with_different_names_is_allowed()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent1").Add<TestEvent>("MyEvent2");

        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent1"));
        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent2"));
    }

    [Fact]
    public void Add_register_duplicate_name_with_different_type_throws_exception()
    {
        var exception = Assert.Throws<ArgumentException>(() => _eventNameRegistry.Add<TestEvent>("MyEvent").Add<OtherEvent>("MyEvent"));
        Assert.Contains("An event with the name 'MyEvent' is already registered with type 'M.EventBrokerSlim.Tests.EventNameRegistryTests+TestEvent'.", exception.Message);
    }

    [Fact]
    public void Add_register_with_null_name_throws_exception()
    {
        Assert.Throws<ArgumentNullException>(() => _eventNameRegistry.Add<TestEvent>(null!));
    }

    [Fact]
    public void Add_register_with_empty_name_throws_exception()
    {
        Assert.Throws<ArgumentException>(() => _eventNameRegistry.Add<TestEvent>(string.Empty));
    }

    [Fact]
    public void Get_with_null_name_throws_exception()
    {
        Assert.Throws<ArgumentNullException>(() => _eventNameRegistry.GetEventType(null!));
    }

    [Fact]
    public void Get_no_registration_for_name_returns_null()
    {
        Assert.Null(_eventNameRegistry.GetEventType("abc"));
    }

    private record TestEvent;
    private record OtherEvent;
}
