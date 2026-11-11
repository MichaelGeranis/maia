using Maia.Presentation.Navigation;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// The bottom bar and back arrows never push a screen twice, Add does nothing on the Add screen, and a navigation that
/// fails never ends Maia.
/// </summary>
public sealed class NavigationGateTests : IDisposable
{
    private readonly RecordingErrorReporter _errors = new();
    private readonly NavigationGate _gate;

    public NavigationGateTests()
    {
        _gate = new NavigationGate(_errors);
    }

    public void Dispose() => _gate.Dispose();

    [Fact]
    public async Task A_second_tap_while_the_first_is_on_its_way_is_dropped()
    {
        var arriving = new TaskCompletionSource();
        var pushed = 0;

        var first = _gate.TapAsync("add", () => "//main/plan", () =>
        {
            pushed++;
            return arriving.Task;
        });
        var second = await _gate.TapAsync("add", () => "//main/plan", () =>
        {
            pushed++;
            return Task.CompletedTask;
        });
        arriving.SetResult();

        Assert.True(await first);
        Assert.False(second);
        Assert.Equal(1, pushed);
    }

    [Fact]
    public async Task Add_on_the_add_screen_leaves_her_form_as_it_is()
    {
        var pushed = false;

        var went = await _gate.TapAsync("add", () => "//main/plan/add", () =>
        {
            pushed = true;
            return Task.CompletedTask;
        });

        Assert.False(went);
        Assert.False(pushed);
    }

    [Theory]
    [InlineData("//main/plan/add", "add", true)]
    [InlineData("//main/week", "//main/week", true)]
    [InlineData("//main/settings", "//main/settings", true)]
    [InlineData("//main/settings/about", "//main/settings", false)]
    [InlineData("//main/week/add", "//main/settings", false)]
    [InlineData("//main/week/tasks", "//main/week", false)]
    [InlineData("//main/week", "//main/plan", false)]
    [InlineData("//main/settings/about", "..", false)]
    [InlineData(null, "settings", false)]
    public void Knows_when_she_is_already_there(string? location, string route, bool already) =>
        Assert.Equal(already, NavigationGate.IsAt(location, route));

    [Fact]
    public async Task A_navigation_that_fails_is_reported_by_code_and_the_next_tap_still_works()
    {
        var failed = await _gate.TapAsync("//main/settings", () => "//main/week", () => Task.FromException(new InvalidOperationException("Shell was busy")));
        var next = await _gate.TapAsync("//main/settings", () => "//main/week", () => Task.CompletedTask);

        Assert.False(failed);
        Assert.True(next);
        Assert.Equal("nav.settings", Assert.Single(_errors.Reports).Operation);
        Assert.Equal("nav.back", NavigationGate.Operation(NavigationGate.Back));
    }

    [Fact]
    public async Task A_screen_navigation_waits_for_a_tap_on_its_way_instead_of_being_dropped()
    {
        var arriving = new TaskCompletionSource();
        var order = new List<string>();

        var tap = _gate.TapAsync("//main/week", () => "//main/plan", () =>
        {
            order.Add("week");
            return arriving.Task;
        });
        var screen = _gate.GoAsync("//setup", () => "//main/week", () =>
        {
            order.Add("setup");
            return Task.CompletedTask;
        });
        Assert.False(screen.IsCompleted);

        arriving.SetResult();

        Assert.True(await tap);
        Assert.True(await screen);
        Assert.Equal(new[] { "week", "setup" }, order);
    }
}
