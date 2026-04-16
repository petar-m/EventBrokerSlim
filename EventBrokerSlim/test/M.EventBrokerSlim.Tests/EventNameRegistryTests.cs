using M.EventBrokerSlim.Persistent;

namespace M.EventBrokerSlim.Tests;

public class EventNameRegistryTests
{
    private readonly EventRegistry _eventNameRegistry = new();

    [Fact]
    public void Add_allows_chaining()
    {
        var returned = _eventNameRegistry.Add<TestEvent>("MyEvent");

        Assert.Same(_eventNameRegistry, returned);
    }

    [Fact]
    public void Add_registers_event_type_with_given_name()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent");

        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent"));
    }

    [Fact]
    public void Add_registers_name_with_given_event_type()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent");

        Assert.Equal("MyEvent", _eventNameRegistry.GetEventName<TestEvent>());
    }

    [Fact]
    public void Add_registering_event_twice_is_a_no_op()
    {
        _ = _eventNameRegistry.Add<TestEvent>("MyEvent").Add<TestEvent>("MyEvent");

        Assert.Equal(typeof(TestEvent), _eventNameRegistry.GetEventType("MyEvent"));
    }

    [Fact]
    public void Add_register_event_twice_with_different_names_throws_exception()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _eventNameRegistry.Add<TestEvent>("MyEvent1").Add<TestEvent>("MyEvent2"));
        Assert.Contains("Can't register event with name 'MyEvent2'. A registry entry for type 'M.EventBrokerSlim.Tests.EventNameRegistryTests+TestEvent' already exists: (MyEvent1, M.EventBrokerSlim.Tests.EventNameRegistryTests+TestEvent).",
            exception.Message);
    }

    [Fact]
    public void Add_register_name_twice_with_with_different_type_throws_exception()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _eventNameRegistry.Add<TestEvent>("MyEvent").Add<OtherEvent>("MyEvent"));
        Assert.Contains("Can't register event with type 'M.EventBrokerSlim.Tests.EventNameRegistryTests+OtherEvent'. A registry entry for name 'MyEvent' already exists: (MyEvent, M.EventBrokerSlim.Tests.EventNameRegistryTests+TestEvent).", 
            exception.Message);
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
    public void GetEventType_with_null_name_throws_exception()
    {
        Assert.Throws<ArgumentNullException>(() => _eventNameRegistry.GetEventType(null!));
    }

    [Fact]
    public void GetEventType_no_registration_for_name_returns_null()
    {
        Assert.Null(_eventNameRegistry.GetEventType("abc"));
    }

    [Fact]
    public void GetEventName_no_registration_for_type_returns_null()
    {
        Assert.Null(_eventNameRegistry.GetEventName<OtherEvent>());
    }

    private record TestEvent;
    private record OtherEvent;
}
