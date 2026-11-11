using System.Text.RegularExpressions;

namespace Maia.Presentation.Tests;

/// <summary>
/// Guards in the code that no view-model test can see: page events never run work that could end Maia, every
/// navigation passes the navigation gate, drawn controls run their command only while it can run, a toast comes after
/// the navigation it belongs to, and no screen is veiled by a focused container or shown while Maia is locked.
/// </summary>
public sealed class AppGuardTests
{
    [Fact]
    public void No_page_or_control_runs_async_work_that_could_end_maia()
    {
        var offenders = Repo.AppSources().Where(file => Repo.CodeOf(file).Contains("async void", StringComparison.Ordinal));

        Assert.Empty(offenders.Select(Path.GetFileName));
    }

    [Fact]
    public void Every_navigation_goes_through_the_navigation_gate()
    {
        var offenders = Repo.AppSources()
            .Where(file => Path.GetFileName(file) != "ShellNavigator.cs")
            .Where(file => Repo.CodeOf(file).Contains("Shell.Current.GoToAsync", StringComparison.Ordinal));

        Assert.Empty(offenders.Select(Path.GetFileName));
    }

    [Fact]
    public void A_toast_is_raised_once_the_next_screen_shows_never_on_the_screen_that_is_closing()
    {
        // The screen still showing takes a toast raised just before it closes, and it goes with it (Delete everything
        // lost its "Everything is deleted" this way). Navigate first, then say so.
        var presentation = Path.Combine(Repo.Root(), "src", "Maia.Presentation");
        var offenders = Directory.GetFiles(presentation, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(Repo.CodeOf(file), @"_notifier\.Show\([^;]*\);\s*await _navigator\.Go\w*Async\("));

        Assert.Empty(offenders.Select(Path.GetFileName));
    }

    [Fact]
    public void A_refusal_or_an_error_is_never_toasted_with_the_success_tick()
    {
        // "Your list is full", "Something went wrong", a screen that couldn't be shown: a calm notice on Sand with no tick
        // (ShowNotice), never the pine toast that confirms something done.
        var presentation = Path.Combine(Repo.Root(), "src", "Maia.Presentation");
        var offenders = Directory.GetFiles(presentation, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(Repo.CodeOf(file), @"[Nn]otifier\.Show\((error|failed|failure|notice|message|result\.Message)\b"));
        var toast = Repo.CodeOf(Repo.App("Controls", "ToastView.cs"));

        Assert.Empty(offenders.Select(Path.GetFileName));
        Assert.Contains("_tick.IsVisible = !toast.IsNotice;", toast, StringComparison.Ordinal);
        Assert.Contains("Palette.Get(toast.IsNotice ? \"Sand\" : \"Pine\")", toast, StringComparison.Ordinal);
        Assert.Contains("Palette.Get(toast.IsNotice ? \"SandDeep\" : \"White\")", toast, StringComparison.Ordinal);
    }

    [Fact]
    public void Maias_screens_are_never_veiled_by_a_focused_container_and_hide_while_locked()
    {
        var activity = Repo.CodeOf(Repo.App("Platforms", "Android", "MainActivity.cs"));
        var presenter = Repo.CodeOf(Repo.App("Services", "AppLockPresenter.cs"));

        // Android's default focus highlight greyed every screen after a key-press unlock (see FocusVeil).
        Assert.Contains("FocusVeil.KeepOffScreenContainers(this)", activity, StringComparison.Ordinal);

        // Shell's Android view ignores the page's Opacity, so covering her pages sets the view's own alpha.
        Assert.Matches(@"view\.Alpha = cover \? 0 : 1", presenter);
    }

    [Fact]
    public void Drawn_controls_run_their_command_only_while_it_can_run()
    {
        var offenders = Directory.GetFiles(Repo.App("Controls"), "*.cs")
            .Where(file => Regex.IsMatch(Repo.CodeOf(file), "Command\\?\\.Execute\\("));

        Assert.Empty(offenders.Select(Path.GetFileName));
    }
}
