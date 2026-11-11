using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>
/// Her blood pressure readings: logged as her monitor shows them at the day and time she took them, kept in that order,
/// never changed, and deleted for good. Numbers no monitor shows are refused as mistyped; nothing is read into the rest.
/// </summary>
public sealed class BloodPressureTests
{
    private static readonly DateOnly Day = new(2026, 10, 10);

    [Fact]
    public void A_reading_is_kept_as_she_logged_it_to_the_minute()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        var id = plan.LogBloodPressure(118, 76, Day, new TimeOnly(8, 30, 45));

        var reading = Assert.Single(plan.BloodPressure);
        Assert.Equal((id, 118, 76, Day, new TimeOnly(8, 30)), (reading.Id, reading.Systolic, reading.Diastolic, reading.Date, reading.Time));
        Assert.Equal(revision + 1, plan.Revision);
        Assert.Empty(plan.PendingEvents);
    }

    [Fact]
    public void Readings_stand_by_the_day_and_time_she_took_them_whatever_order_she_logged_them_in()
    {
        var plan = TestData.Plan();

        var evening = plan.LogBloodPressure(124, 80, Day, new TimeOnly(20, 0));
        var earlier = plan.LogBloodPressure(120, 78, Day.AddDays(-1), new TimeOnly(21, 0));
        var morning = plan.LogBloodPressure(118, 76, Day, new TimeOnly(8, 0));
        var alsoMorning = plan.LogBloodPressure(119, 77, Day, new TimeOnly(8, 0));

        Assert.Equal([earlier, morning, alsoMorning, evening], plan.BloodPressure.Select(r => r.Id));
    }

    [Theory]
    [InlineData(BloodPressureReading.MinSystolic, BloodPressureReading.MinDiastolic)]
    [InlineData(BloodPressureReading.MaxSystolic, BloodPressureReading.MaxDiastolic)]
    [InlineData(81, 80)]
    public void Any_two_numbers_a_monitor_can_show_are_a_reading(int systolic, int diastolic)
    {
        Assert.True(BloodPressureReading.IsValid(systolic, diastolic));
        TestData.Plan().LogBloodPressure(systolic, diastolic, Day, new TimeOnly(9, 0));
    }

    [Theory]
    [InlineData(BloodPressureReading.MinSystolic - 1, 40)]
    [InlineData(BloodPressureReading.MaxSystolic + 1, 80)]
    [InlineData(120, BloodPressureReading.MinDiastolic - 1)]
    [InlineData(250, BloodPressureReading.MaxDiastolic + 1)]
    [InlineData(80, 80)]
    [InlineData(76, 118)]
    public void Numbers_no_monitor_shows_are_refused_as_mistyped(int systolic, int diastolic)
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        var ex = Assert.Throws<DomainException>(() => plan.LogBloodPressure(systolic, diastolic, Day, new TimeOnly(9, 0)));

        Assert.Equal("reading.values.invalid", ex.Code);
        Assert.Empty(plan.BloodPressure);
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_reading_is_deleted_for_good_and_one_already_gone_is_said_to_be()
    {
        var plan = TestData.Plan();
        var kept = plan.LogBloodPressure(118, 76, Day, new TimeOnly(8, 0));
        var gone = plan.LogBloodPressure(150, 95, Day, new TimeOnly(9, 0));
        var revision = plan.Revision;

        plan.RemoveBloodPressure(gone);

        Assert.Equal(kept, Assert.Single(plan.BloodPressure).Id);
        Assert.Equal(revision + 1, plan.Revision);
        Assert.Equal("reading.not_found", Assert.Throws<DomainException>(() => plan.RemoveBloodPressure(gone)).Code);
    }

    [Fact]
    public void A_full_list_takes_no_more_readings()
    {
        var plan = TestData.Plan();
        var stored = Enumerable.Range(0, PregnancyPlan.MaxReadings)
            .Select(i => PregnancyPlan.RehydrateReading(ReadingId.New(), 120, 80, Day, new TimeOnly(i / 60 % 24, i % 60)));
        var full = PregnancyPlan.Rehydrate(plan.Id, plan.Dating, plan.Template, plan.Language, plan.CreatedAt, plan.Checks, plan.Tasks, plan.CareTeam, plan.Revision, readings: stored);

        var ex = Assert.Throws<DomainException>(() => full.LogBloodPressure(118, 76, Day, new TimeOnly(9, 0)));

        Assert.Equal("plan.readings.full", ex.Code);
        Assert.Equal(PregnancyPlan.MaxReadings, full.BloodPressure.Count);
    }

    [Fact]
    public void A_stored_reading_comes_back_as_it_was_saved_and_stands_in_order()
    {
        var plan = TestData.Plan();
        var later = PregnancyPlan.RehydrateReading(ReadingId.New(), 400, 300, Day, new TimeOnly(10, 0, 30));
        var sooner = PregnancyPlan.RehydrateReading(ReadingId.New(), 118, 76, Day, new TimeOnly(9, 0));

        var loaded = PregnancyPlan.Rehydrate(plan.Id, plan.Dating, plan.Template, plan.Language, plan.CreatedAt, plan.Checks, plan.Tasks, plan.CareTeam, plan.Revision, readings: [later, sooner]);

        Assert.Equal([sooner, later], loaded.BloodPressure);
        Assert.Equal((400, 300, new TimeOnly(10, 0, 30)), (loaded.BloodPressure[1].Systolic, loaded.BloodPressure[1].Diastolic, loaded.BloodPressure[1].Time));
    }

    [Fact]
    public void A_reading_id_reads_as_its_guid_without_dashes()
    {
        var id = ReadingId.New();

        Assert.Equal(id.Value.ToString("N"), id.ToString());
        Assert.NotEqual(id, ReadingId.New());
    }
}
