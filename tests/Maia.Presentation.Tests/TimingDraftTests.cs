using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>"When" as she fills it in on Add and in Edit on Journey: what it starts at and what it will take.</summary>
public sealed class TimingDraftTests : IDisposable
{
    private static readonly GestationalAge Age = PregnancyDating.FromDueDate(TestData.DueDate).On(TestData.Today);
    private static readonly DateTime First = new(2026, 4, 9);
    private static readonly DateTime Last = new(2027, 1, 31);

    private readonly CultureScope _culture = new("en-US");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void A_last_day_before_the_first_is_the_first()
    {
        var timing = new TimingDraft();

        timing.Start(true, true, TimingMode.DateTime, First, Age, First, First.AddDays(-3));

        Assert.Equal((First, First, First), (timing.MinimumDate, timing.MaximumDate, timing.Date));
    }

    [Fact]
    public void A_day_outside_the_pregnancy_starts_at_its_nearest_end()
    {
        var timing = new TimingDraft();

        timing.Start(true, true, TimingMode.DateTime, First.AddDays(-10), Age, First, Last);
        Assert.Equal(First, timing.Date);

        timing.Start(true, true, TimingMode.DateTime, Last.AddDays(10).AddHours(9), Age, First, Last);
        Assert.Equal((Last, TimeSpan.FromHours(9)), (timing.Date, timing.Time));
    }

    [Theory]
    [InlineData(Trimester.First, true, false, false)]
    [InlineData(Trimester.Second, false, true, false)]
    [InlineData(Trimester.Third, false, false, true)]
    public void A_trimester_given_is_the_one_chosen(Trimester trimester, bool first, bool second, bool third)
    {
        var timing = new TimingDraft();

        timing.Start(true, true, TimingMode.Week, First, Age, First, Last, trimester);

        Assert.Equal(trimester, timing.Trimester);
        Assert.Equal((first, second, third), (timing.IsFirstTrimester, timing.IsSecondTrimester, timing.IsThirdTrimester));
    }

    [Fact]
    public void Nothing_chosen_is_no_when_at_all()
    {
        var timing = new TimingDraft();

        timing.Start(true, true, null, First, Age, First, Last);

        Assert.Null(timing.ToWhen(done: true));
        Assert.Null(timing.Trimester);
    }

    [Fact]
    public void A_trimester_reads_as_done_in_it_or_waiting_in_it()
    {
        var timing = new TimingDraft();
        timing.Start(false, true, TimingMode.Trimester, First, Age, First, Last);

        Assert.Equal(EntryWhen.DoneIn(Trimester.Second), timing.ToWhen(done: true));
        Assert.Equal(EntryWhen.NotBookedIn(Trimester.Second), timing.ToWhen(done: false));
    }

    [Theory]
    [InlineData("Never")]
    [InlineData("7")]
    [InlineData("DateTime")]
    public void A_way_of_saying_when_that_is_not_offered_is_not_chosen(string mode)
    {
        var timing = new TimingDraft();
        timing.Start(offersDateTime: false, offersPeriod: true, TimingMode.Week, First, Age, First, Last);

        timing.ChooseModeCommand.Execute(mode);

        Assert.Equal(TimingMode.Week, timing.Mode);
    }

    [Theory]
    [InlineData("Fourth")]
    [InlineData("9")]
    public void A_trimester_that_does_not_exist_is_not_chosen(string trimester)
    {
        var timing = new TimingDraft();
        timing.Start(false, true, TimingMode.Trimester, First, Age, First, Last);

        timing.ChooseTrimesterCommand.Execute(trimester);

        Assert.Equal(Trimester.Second, timing.Trimester);
    }
}
