using System.Collections.ObjectModel;

namespace Maia.Domain.Common;

/// <summary>Something that happened in the domain that other parts of the app react to.</summary>
public interface IDomainEvent;

/// <summary>
/// Base class for aggregates. Collects domain events raised by state changes so the application
/// layer can dispatch them after the aggregate has been saved (for example, setting reminders again).
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = [];
    private readonly ReadOnlyCollection<IDomainEvent> _eventsView;

    protected AggregateRoot()
    {
        _eventsView = _events.AsReadOnly();
    }

    /// <summary>
    /// Incremented on every change. Persisted, so a future sync backend can detect conflicts.
    /// </summary>
    public long Revision { get; protected set; }

    public IReadOnlyList<IDomainEvent> PendingEvents => _eventsView;

    /// <summary>Returns and clears the events raised since the last call.</summary>
    public IReadOnlyList<IDomainEvent> DequeueEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    protected void Raise(IDomainEvent domainEvent)
    {
        _events.Add(domainEvent);
        Revision++;
    }

    protected void Touch() => Revision++;
}
