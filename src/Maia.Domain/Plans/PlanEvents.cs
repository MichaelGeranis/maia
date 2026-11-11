using Maia.Domain.Common;

namespace Maia.Domain.Plans;

/// <summary>A check now has an appointment, new or moved.</summary>
public sealed record CheckBooked(CheckId CheckId, Appointment Appointment) : IDomainEvent;

/// <summary>A check no longer has a booking: it was moved to a trimester or a week.</summary>
public sealed record CheckBookingCleared(CheckId CheckId) : IDomainEvent;

/// <summary>A result was removed; its stored file (if any) can be deleted.</summary>
public sealed record ResultRemoved(AttachmentId AttachmentId, string? BlobId) : IDomainEvent;

public sealed record TaskScheduled(TaskId TaskId) : IDomainEvent;

/// <summary>A task was completed, removed or lost its time, so it no longer has a reminder.</summary>
public sealed record TaskUnscheduled(TaskId TaskId) : IDomainEvent;

public sealed record DueDateChanged(DateOnly Previous, DateOnly Current) : IDomainEvent;

/// <summary>The plan's language changed, so check titles (and reminder titles) read differently.</summary>
public sealed record PlanLanguageChanged(string Language) : IDomainEvent;

/// <summary>A contact was linked to or unlinked from her care team, so who an appointment is with may read differently.</summary>
public sealed record CareTeamChanged : IDomainEvent;

/// <summary>
/// The plan took the wording (and, where she had not acted, the weeks) of a newer version of its template, so check
/// titles and reminder titles may read differently.
/// </summary>
public sealed record PlanTemplateUpdated(int PreviousVersion, int Version) : IDomainEvent;

/// <summary>An entry was deleted from her journey, with its booking if it had one.</summary>
public sealed record CheckDeleted(CheckId CheckId) : IDomainEvent;

/// <summary>An entry has a new name, so its reminders read differently.</summary>
public sealed record CheckRenamed(CheckId CheckId) : IDomainEvent;
