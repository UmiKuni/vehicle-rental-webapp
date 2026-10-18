using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class DomainPrimitivesTests
{
    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredAt, string Message) : DomainEvent(EventId, OccurredAt);

    private sealed class TestEntity : Entity<Guid>
    {
        public string Name { get; }

        public TestEntity(Guid id, string name) : base(id)
        {
            Name = name;
        }
    }

    private sealed class OtherEntity : Entity<Guid>
    {
        public OtherEntity(Guid id) : base(id) { }
    }

    private sealed class TestAggregateRoot : AggregateRoot<Guid>
    {
        public TestAggregateRoot(Guid id) : base(id) { }

        public void DoSomething(string message)
        {
            Raise(new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, message));
        }
    }

    [Fact]
    public void Entity_SameIdAndType_AreEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "First");
        var e2 = new TestEntity(id, "Second");

        (e1 == e2).Should().BeTrue();
        e1.Equals(e2).Should().BeTrue();
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    [Fact]
    public void Entity_DifferentTypesSameId_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "First");
        var e2 = new OtherEntity(id);

        (e1 == e2).Should().BeFalse();
        e1.Equals(e2).Should().BeFalse();
    }

    [Fact]
    public void AggregateRoot_RaisesAndClearsDomainEvents()
    {
        var aggregate = new TestAggregateRoot(Guid.NewGuid());
        aggregate.DomainEvents.Should().BeEmpty();

        aggregate.DoSomething("Action 1");
        aggregate.DoSomething("Action 2");

        aggregate.DomainEvents.Should().HaveCount(2);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Guard_Against_ThrowsWhenConditionTrue()
    {
        var act = () => Guard.Against(true, "guard.failed", "Condition met");

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("guard.failed");
    }

    [Fact]
    public void Guard_AgainstNull_ThrowsWhenNull()
    {
        string? nullStr = null;
        var act = () => Guard.AgainstNull(nullStr, "guard.null", "Null value");

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("guard.null");
    }

    [Fact]
    public void Guard_AgainstOutOfRange_ThrowsWhenOutsideLimits()
    {
        var act = () => Guard.AgainstOutOfRange(10, 1, 5, "guard.out_of_range", "Exceeded");

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("guard.out_of_range");
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly HashSet<string> _store = new();

        public Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Contains($"{eventId}_{consumerName}"));
        }

        public Task MarkAsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _store.Add($"{eventId}_{consumerName}");
            return Task.CompletedTask;
        }
    }

    private sealed record TestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IntegrationEvent(EventId, OccurredAt);

    private sealed class TestIdempotentHandler : IdempotentHandler<TestIntegrationEvent>
    {
        public int ExecutionCount { get; private set; }

        public TestIdempotentHandler(IIdempotencyStore store) : base(store) { }

        protected override Task HandleCoreAsync(TestIntegrationEvent @event, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task IdempotentHandler_ProcessesEventOnlyOnce()
    {
        var store = new InMemoryIdempotencyStore();
        var handler = new TestIdempotentHandler(store);
        var @event = new TestIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        handler.ExecutionCount.Should().Be(1);
    }
}
