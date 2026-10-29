using FluxKit.Core.Utilities;
using Serilog;

namespace FluxKit.Core.Tests;

public sealed class UtilityRegistryTests
{
    private readonly Dictionary<string, bool> _enabled = [];
    private readonly UtilityRegistry _registry;

    public UtilityRegistryTests()
    {
        _registry = new UtilityRegistry(
            id => _enabled.GetValueOrDefault(id, true),
            (id, value) =>
            {
                _enabled[id] = value;
                return Task.CompletedTask;
            },
            new LoggerConfiguration().CreateLogger());
    }

    private sealed class FakeUtility(string id, int order, bool failOnStart = false) : IFluxUtility
    {
        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public List<string> Journal { get; init; } = [];

        public UtilityDescriptor Descriptor { get; } = new(id, id, "test", "", order);

        public UtilityState State { get; private set; } = UtilityState.Disabled;

        public event EventHandler<UtilityState>? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Starts++;
            if (failOnStart)
            {
                throw new InvalidOperationException("boom");
            }

            State = UtilityState.Running;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            Journal.Add("stop:" + Descriptor.Id);
            State = UtilityState.Disabled;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Journal.Add("dispose:" + Descriptor.Id);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void Utilities_are_ordered_by_descriptor_order()
    {
        _registry.Register(new FakeUtility("b", 20));
        _registry.Register(new FakeUtility("a", 10));

        Assert.Equal(["a", "b"], _registry.All.Select(u => u.Descriptor.Id));
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        _registry.Register(new FakeUtility("a", 10));

        Assert.Throws<InvalidOperationException>(() => _registry.Register(new FakeUtility("a", 20)));
    }

    [Fact]
    public async Task Only_enabled_utilities_start()
    {
        var on = new FakeUtility("on", 1);
        var off = new FakeUtility("off", 2);
        _enabled["off"] = false;
        _registry.Register(on);
        _registry.Register(off);

        await _registry.StartEnabledAsync(CancellationToken.None);

        Assert.Equal(1, on.Starts);
        Assert.Equal(0, off.Starts);
    }

    [Fact]
    public async Task SetEnabled_persists_and_starts_or_stops_exactly_once()
    {
        var utility = new FakeUtility("a", 1);
        _registry.Register(utility);

        await _registry.SetEnabledAsync("a", true);
        await _registry.SetEnabledAsync("a", true);
        await _registry.SetEnabledAsync("a", false);
        await _registry.SetEnabledAsync("a", false);

        Assert.Equal(1, utility.Starts);
        Assert.Equal(1, utility.Stops);
        Assert.False(_enabled["a"]);
    }

    [Fact]
    public async Task Start_failure_does_not_throw()
    {
        _registry.Register(new FakeUtility("bad", 1, failOnStart: true));

        Exception? error = await Record.ExceptionAsync(() => _registry.StartEnabledAsync(CancellationToken.None));

        Assert.Null(error);
    }

    [Fact]
    public async Task Dispose_stops_and_disposes_in_reverse_order()
    {
        var journal = new List<string>();
        _registry.Register(new FakeUtility("a", 1) { Journal = journal });
        _registry.Register(new FakeUtility("b", 2) { Journal = journal });
        await _registry.StartEnabledAsync(CancellationToken.None);

        await _registry.DisposeAsync();

        Assert.Equal(["stop:b", "stop:a", "dispose:b", "dispose:a"], journal);
    }
}
