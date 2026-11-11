using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Settings;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class DueDateSettingsTests : IDisposable
{
    private static readonly DateOnly AfterTheScan = TestData.DueDate.AddDays(9);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private async Task<DueDateSettings> OpenAsync()
    {
        var settings = _app.CreateSettings();
        await settings.LoadAsync();
        return settings.DueDate;
    }

    [Fact]
    public async Task Settings_shows_her_due_date_and_how_far_along_she_is()
    {
        await _app.CreatePlanAsync();

        var card = await OpenAsync();

        Assert.True(card.HasPlan);
        Assert.Equal("Due Sun 17 Jan · Week 24 + 3", card.Current);
        Assert.False(card.IsEditing);
    }

    [Fact]
    public async Task Changing_the_due_date_after_a_scan_moves_her_plan_and_keeps_her_bookings()
    {
        await _app.CreatePlanAsync();
        var booking = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));
        await _app.Plans.BookCheckAsync(TestData.Glucose, booking);
        var card = await OpenAsync();

        card.EditCommand.Execute(null);
        Assert.True(card.IsEditing);
        Assert.Equal(TestData.DueDate, card.Entry.EffectiveDueDate);
        Assert.False(card.SaveCommand.CanExecute(null));

        card.Entry.DueDate = AfterTheScan.ToDateTime(TimeOnly.MinValue);
        Assert.Equal("You're 23 weeks and 1 day today. Due Tue 26 Jan.", card.Entry.Summary);
        await card.SaveCommand.ExecuteAsync(null);

        var plan = _app.Repository.Stored!;
        Assert.Equal(AfterTheScan, plan.Dating.DueDate);
        Assert.Equal(booking, plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Equal(CheckState.Booked, plan.GetCheck(TestData.Glucose).State);
        Assert.Equal(GestationalAge.FromWeeks(23, 1), plan.AgeOn(TestData.Today));
        Assert.False(card.IsEditing);
        Assert.Equal("Due Tue 26 Jan · Week 23 + 1", card.Current);
        Assert.Equal("Your due date is now Tue 26 Jan. Your journey moved with it.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task The_new_date_can_come_from_the_first_day_of_her_last_period()
    {
        await _app.CreatePlanAsync();
        var card = await OpenAsync();
        card.EditCommand.Execute(null);

        card.Entry.ToggleDateModeCommand.Execute(null);
        card.Entry.LastPeriodDate = AfterTheScan.AddDays(-PregnancyDating.TermDays).ToDateTime(TimeOnly.MinValue);
        await card.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AfterTheScan, _app.Repository.Stored!.Dating.DueDate);
    }

    [Fact]
    public async Task A_date_that_does_not_fit_a_current_pregnancy_is_not_saved()
    {
        await _app.CreatePlanAsync();
        var card = await OpenAsync();
        card.EditCommand.Execute(null);

        card.Entry.DueDate = new DateTime(2027, 12, 1);

        Assert.False(card.Entry.IsValid);
        Assert.Equal("That date doesn't match a current pregnancy. Check it and try again.", card.Entry.Summary);
        Assert.False(card.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_leaves_the_due_date_as_it_was()
    {
        await _app.CreatePlanAsync();
        var card = await OpenAsync();
        card.EditCommand.Execute(null);
        card.Entry.DueDate = AfterTheScan.ToDateTime(TimeOnly.MinValue);

        card.CancelCommand.Execute(null);

        Assert.False(card.IsEditing);
        Assert.Equal(TestData.DueDate, _app.Repository.Stored!.Dating.DueDate);

        card.EditCommand.Execute(null);
        Assert.Equal(TestData.DueDate, card.Entry.EffectiveDueDate);
    }

    [Fact]
    public async Task If_the_plan_cannot_be_saved_she_is_told_and_the_date_stays()
    {
        await _app.CreatePlanAsync();
        var card = await OpenAsync();
        card.EditCommand.Execute(null);
        card.Entry.DueDate = AfterTheScan.ToDateTime(TimeOnly.MinValue);
        _app.Repository.FailNextSave = new IOException("disk full");

        await card.SaveCommand.ExecuteAsync(null);

        Assert.True(card.IsEditing);
        Assert.Equal("Something went wrong. Please try again.", _app.Notifier.Shown.Single());
        Assert.Equal("settings.due_date", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Equal(TestData.DueDate, _app.Repository.Stored!.Dating.DueDate);
    }

    [Fact]
    public async Task Without_a_plan_there_is_nothing_to_change()
    {
        var card = await OpenAsync();

        Assert.False(card.HasPlan);
        Assert.False(card.EditCommand.CanExecute(null));
    }
}
