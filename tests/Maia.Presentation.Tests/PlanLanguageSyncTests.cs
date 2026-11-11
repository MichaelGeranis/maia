using Maia.Application.Abstractions;
using Maia.Domain.Plans;
using Maia.Presentation.Localization;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>At start-up the plan's language (check titles, reminders) follows the language Maia shows.</summary>
public sealed class PlanLanguageSyncTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_plan_made_in_english_follows_a_phone_now_in_greek()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        using var greek = new CultureScope("el-GR");

        await new PlanLanguageSync(_app.Plans, _app.Errors).ApplyAsync();

        Assert.Equal("el", _app.Repository.Stored!.Language);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Nothing_is_saved_when_they_already_match()
    {
        await _app.CreatePlanAsync();
        var saves = _app.Repository.SaveCount;

        await new PlanLanguageSync(_app.Plans, _app.Errors).ApplyAsync();

        Assert.Equal(saves, _app.Repository.SaveCount);
    }

    [Fact]
    public async Task Without_a_plan_nothing_happens()
    {
        await new PlanLanguageSync(_app.Plans, _app.Errors).ApplyAsync();

        Assert.Null(_app.Repository.Stored);
        Assert.Empty(_app.Errors.Reports);
    }
}
