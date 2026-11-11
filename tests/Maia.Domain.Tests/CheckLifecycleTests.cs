using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>
/// Every entry operation from every state: the state it leaves, or the rule it breaks; the events it raises; and
/// whether it counts as a change. A refused or empty operation raises nothing and keeps the revision.
/// </summary>
public sealed class CheckLifecycleTests
{
    private static readonly Appointment OctoberSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));
    private static readonly Appointment LaterSlot = new(new DateOnly(2026, 10, 14), new TimeOnly(9, 0));

    [Theory]
    [InlineData("NotBooked", "Book", "Booked", "CheckBooked")]
    [InlineData("NotBooked", "NotBookedInTrimester", "NotBooked", "+")]
    [InlineData("NotBooked", "NotBookedInWeek", "NotBooked", "+")]
    [InlineData("NotBooked", "DoneInTrimester", "Done", "+")]
    [InlineData("NotBooked", "DoneInWeek", "Done", "+")]
    [InlineData("NotBooked", "Rename", "NotBooked", "CheckRenamed")]
    [InlineData("NotBooked", "Delete", "Deleted", "CheckDeleted")]
    [InlineData("Booked", "Book", "Booked", "CheckBooked")]
    [InlineData("Booked", "NotBookedInTrimester", "NotBooked", "CheckBookingCleared")]
    [InlineData("Booked", "NotBookedInWeek", "NotBooked", "CheckBookingCleared")]
    [InlineData("Booked", "DoneInTrimester", "Done", "CheckBookingCleared")]
    [InlineData("Booked", "DoneInWeek", "Done", "CheckBookingCleared")]
    [InlineData("Booked", "Rename", "Booked", "CheckRenamed")]
    [InlineData("Booked", "Delete", "Deleted", "CheckDeleted")]
    [InlineData("Done", "Book", "Booked", "CheckBooked")]
    [InlineData("Done", "NotBookedInTrimester", "NotBooked", "+")]
    [InlineData("Done", "NotBookedInWeek", "NotBooked", "+")]
    [InlineData("Done", "DoneInTrimester", "Done", "+")]
    [InlineData("Done", "DoneInWeek", "Done", "+")]
    [InlineData("Done", "Rename", "Done", "CheckRenamed")]
    [InlineData("Done", "Delete", "Deleted", "CheckDeleted")]
    [InlineData("Milestone", "Book", "check.is_milestone", "")]
    [InlineData("Milestone", "NotBookedInTrimester", "check.is_milestone", "")]
    [InlineData("Milestone", "NotBookedInWeek", "check.is_milestone", "")]
    [InlineData("Milestone", "DoneInTrimester", "check.is_milestone", "")]
    [InlineData("Milestone", "DoneInWeek", "check.is_milestone", "")]
    [InlineData("Milestone", "Rename", "check.is_milestone", "")]
    [InlineData("Milestone", "Delete", "check.is_milestone", "")]
    public void Each_operation_from_each_state(string from, string operation, string expected, string events)
    {
        var (plan, id) = PlanWithCheckIn(from);
        var stateBefore = plan.GetCheck(id).State;
        var revision = plan.Revision;

        var error = Record.Exception(() => Apply(plan, id, operation));

        if (expected.Contains('.', StringComparison.Ordinal))
        {
            Assert.Equal(expected, Assert.IsType<DomainException>(error).Code);
            Assert.Equal(stateBefore, plan.GetCheck(id).State);
        }
        else if (expected == "Deleted")
        {
            Assert.Null(error);
            Assert.DoesNotContain(plan.Checks, c => c.Id == id);
        }
        else
        {
            Assert.Null(error);
            Assert.Equal(Enum.Parse<CheckState>(expected), plan.GetCheck(id).State);
        }

        var raised = plan.DequeueEvents().Select(e => e.GetType().Name).ToList();
        var expectedEvents = events.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(e => e != "+").ToList();
        Assert.Equal(expectedEvents, raised);

        // "+" marks a change with no event (it only bumps the revision); "" a refusal or a no-op.
        var changed = events.Length > 0;
        Assert.Equal(changed, plan.Revision != revision);
    }

    [Theory]
    [InlineData("NotBooked")]
    [InlineData("Booked")]
    [InlineData("Done")]
    public void Saving_the_same_timing_again_changes_nothing(string state)
    {
        var (plan, id) = PlanWithCheckIn(state);
        var when = state switch
        {
            "Booked" => EntryWhen.Booked(OctoberSlot),
            "Done" => EntryWhen.DoneInWeek(24),
            _ => EntryWhen.NotBookedIn(Trimester.Second),
        };
        plan.Reschedule(id, when);
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.Reschedule(id, when);

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void Moving_an_entry_keeps_its_name_and_renaming_it_keeps_its_timing()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, OctoberSlot);

        plan.Rename(TestData.Glucose, LocalizedText.Plain("My sugar test"));
        plan.Reschedule(TestData.Glucose, EntryWhen.Booked(LaterSlot));

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Equal("My sugar test", check.Title.In("en"));
        Assert.Equal(LaterSlot, check.Appointment);
    }

    private static (PregnancyPlan Plan, CheckId Id) PlanWithCheckIn(string state)
    {
        var plan = TestData.Plan();
        var id = TestData.Glucose;
        switch (state)
        {
            case "Booked":
                plan.Book(id, OctoberSlot);
                break;
            case "Done":
                plan.Reschedule(id, EntryWhen.DoneInWeek(24));
                break;
            case "Milestone":
                id = TestData.DueDateMilestone;
                break;
            default:
                break;
        }

        plan.DequeueEvents();
        return (plan, id);
    }

    private static void Apply(PregnancyPlan plan, CheckId id, string operation)
    {
        switch (operation)
        {
            case "Book":
                plan.Book(id, LaterSlot);
                break;
            case "NotBookedInTrimester":
                plan.Reschedule(id, EntryWhen.NotBookedIn(Trimester.Third));
                break;
            case "NotBookedInWeek":
                plan.Reschedule(id, EntryWhen.NotBookedInWeek(30));
                break;
            case "DoneInTrimester":
                plan.Reschedule(id, EntryWhen.DoneIn(Trimester.Second));
                break;
            case "DoneInWeek":
                plan.Reschedule(id, EntryWhen.DoneInWeek(25));
                break;
            case "Rename":
                plan.Rename(id, LocalizedText.Plain("Sugar test"));
                break;
            case "Delete":
                plan.Delete(id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation.");
        }
    }
}
