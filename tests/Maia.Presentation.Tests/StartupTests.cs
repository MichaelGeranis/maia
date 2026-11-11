using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Results;
using Maia.Presentation.Startup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class StartupTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private StartupViewModel Create(PlanQueries? queries = null) =>
        new(queries ?? _app.Queries, _app.Plans, _app.Setup, _app.Navigator, _app.Notifier, _app.Clock, _app.Errors, new PlanLanguageSync(_app.Plans, _app.Errors), _app.Shares, _app.Viewer);

    /// <summary>Her plan and a result file are on this phone, but the plan can't be opened.</summary>
    private async Task<StartupViewModel> OpenAPlanThePhoneCannotOpenAsync()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach("scan.pdf", "application/pdf", new byte[] { 1, 2, 3 }));
        _app.Repository.Unreadable = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));
        var vm = Create();
        await vm.OpenAsync();
        return vm;
    }

    /// <summary>Her plan and a result file are on this phone and readable, but something else stops the plan opening.</summary>
    private async Task<(StartupViewModel Vm, FailingRepository Repository)> OpenAPlanThatFailsToOpenAsync(Exception failure)
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach("scan.pdf", "application/pdf", new byte[] { 1, 2, 3 }));
        var repository = new FailingRepository(_app.Repository) { Failure = failure };
        var vm = Create(new PlanQueries(repository, _app.Clock));
        await vm.OpenAsync();
        return (vm, repository);
    }

    [Fact]
    public async Task Start_up_loads_her_plan_once_before_your_week()
    {
        await _app.CreatePlanAsync();
        var before = _app.Repository.LoadCount;
        var loadsOnTheWay = -1;
        _app.Navigator.WhenVisiting = _ => loadsOnTheWay = _app.Repository.LoadCount - before;

        await Create().OpenAsync();

        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal(1, loadsOnTheWay);
    }

    [Fact]
    public async Task Opens_her_week_when_she_has_a_plan()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.OpenAsync();

        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Opening_a_plan_saved_before_the_home_time_zone_gives_it_the_phones()
    {
        await _app.Repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        var vm = Create();

        await vm.OpenAsync();

        Assert.Equal("Europe/Athens", _app.Repository.Stored!.TimeZoneId);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Her_plan_opens_once_when_the_start_up_screen_shows_again_while_it_opens()
    {
        // As after unlocking Maia on a cold start: the start-up screen may show again before her plan has opened.
        await _app.CreatePlanAsync();
        var repository = new SlowRepository(_app.Repository);
        var vm = Create(new PlanQueries(repository, _app.Clock));

        var first = vm.OpenAsync();
        var second = vm.OpenAsync();
        repository.Open();
        await Task.WhenAll(first, second);
        await vm.Tidying;

        Assert.Equal(1, repository.Loads);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Coming_back_to_the_start_up_screen_later_looks_again()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.OpenAsync();
        await vm.OpenAsync();

        Assert.Equal(new[] { "main", "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Goes_to_setup_without_a_plan()
    {
        await Create().OpenAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Opening_her_plan_first_renames_it_into_the_language_maia_shows()
    {
        await _app.CreatePlanAsync();
        using var greek = new CultureScope("el-GR");

        await Create().OpenAsync();

        Assert.Equal("el", _app.Repository.Stored!.Language);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task After_a_language_switch_goes_back_where_she_was()
    {
        await _app.CreatePlanAsync();
        var reopened = 0;

        await Create().OpenAsync(() =>
        {
            reopened++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, reopened);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task A_plan_this_phone_cannot_open_gets_a_calm_way_on_and_is_left_as_it_is()
    {
        var vm = await OpenAPlanThePhoneCannotOpenAsync();

        Assert.True(vm.CannotOpen);
        Assert.False(vm.IsOpening);
        Assert.False(vm.NeedsUpdate);
        Assert.Empty(_app.Navigator.Visited);
        Assert.True(vm.RestoreCommand.CanExecute(null));
        Assert.True(vm.StartAgainCommand.CanExecute(null));
        Assert.Equal("startup.key.unavailable", Assert.Single(_app.Errors.Reports).Operation);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Single(_app.Attachments.Blobs);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task Anything_else_that_stops_her_plan_opening_only_offers_to_try_again(Type failure)
    {
        var (vm, _) = await OpenAPlanThatFailsToOpenAsync((Exception)Activator.CreateInstance(failure)!);

        Assert.True(vm.OpenFailed);
        Assert.False(vm.CannotOpen);
        Assert.False(vm.NeedsUpdate);
        Assert.False(vm.IsOpening);
        Assert.False(vm.RestoreCommand.CanExecute(null));
        Assert.False(vm.AskStartAgainCommand.CanExecute(null));
        Assert.False(vm.StartAgainCommand.CanExecute(null));
        Assert.True(vm.TryAgainCommand.CanExecute(null));
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal("startup.load", Assert.Single(_app.Errors.Reports).Operation);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Single(_app.Attachments.Blobs);
    }

    [Fact]
    public async Task A_plan_that_may_be_fine_is_never_restored_over_or_removed()
    {
        var (vm, _) = await OpenAPlanThatFailsToOpenAsync(new IOException("storage not ready"));
        var forgotten = _app.DataKey.Forgotten;

        await vm.RestoreCommand.ExecuteAsync(null);
        vm.AskStartAgainCommand.Execute(null);
        await vm.StartAgainCommand.ExecuteAsync(null);

        Assert.Empty(_app.Navigator.Visited);
        Assert.False(vm.IsConfirmingStartAgain);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Single(_app.Attachments.Blobs);
        Assert.Equal(forgotten, _app.DataKey.Forgotten);
        Assert.DoesNotContain(_app.Errors.Reports, r => r.Operation == "startup.start_again");
    }

    [Fact]
    public async Task Trying_again_after_something_passing_opens_her_plan()
    {
        var (vm, repository) = await OpenAPlanThatFailsToOpenAsync(new IOException("storage not ready"));
        repository.Failure = null;

        await vm.TryAgainCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Trying_again_on_a_plan_that_turns_out_unreadable_offers_the_way_on()
    {
        var (vm, repository) = await OpenAPlanThatFailsToOpenAsync(new IOException("storage not ready"));
        repository.Failure = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));

        await vm.TryAgainCommand.ExecuteAsync(null);

        Assert.True(vm.CannotOpen);
        Assert.False(vm.OpenFailed);
        Assert.True(vm.RestoreCommand.CanExecute(null));
        Assert.True(vm.StartAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_plan_from_a_newer_maia_asks_her_to_update_and_offers_nothing_that_changes_it()
    {
        await _app.CreatePlanAsync();
        _app.Repository.Unreadable = PlanUnreadableException.FromNewerApp();
        var vm = Create();

        await vm.OpenAsync();

        Assert.True(vm.NeedsUpdate);
        Assert.False(vm.CannotOpen);
        Assert.False(vm.RestoreCommand.CanExecute(null));
        Assert.False(vm.StartAgainCommand.CanExecute(null));
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal("startup.plan.newer_app", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task Restore_opens_the_restore_screen()
    {
        var vm = await OpenAPlanThePhoneCannotOpenAsync();

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "restore" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Starting_again_asks_once_more_and_can_be_called_off()
    {
        var vm = await OpenAPlanThePhoneCannotOpenAsync();

        vm.AskStartAgainCommand.Execute(null);
        Assert.True(vm.IsConfirmingStartAgain);

        vm.KeepPlanCommand.Execute(null);

        Assert.False(vm.IsConfirmingStartAgain);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task Starting_again_removes_what_cannot_be_opened_and_goes_to_setup()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        _app.Setup.DueDate = TestData.DueDate;
        _app.Repository.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));
        var vm = Create();
        await vm.OpenAsync();
        var forgotten = _app.DataKey.Forgotten;
        vm.AskStartAgainCommand.Execute(null);

        await vm.StartAgainCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored);
        Assert.Null(_app.Repository.Unreadable);
        Assert.Empty(_app.Attachments.Blobs);
        Assert.Equal(forgotten + 1, _app.DataKey.Forgotten);
        Assert.Equal(1, _app.Viewer.Cleared);
        Assert.Null(_app.Setup.DueDate);
        Assert.False(vm.IsConfirmingStartAgain);
        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task If_starting_again_fails_she_is_told_plainly_and_stays()
    {
        var vm = await OpenAPlanThePhoneCannotOpenAsync();
        _app.DataKey.WhenForgotten = () => throw new IOException("keystore");
        vm.AskStartAgainCommand.Execute(null);

        await vm.StartAgainCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.ErrorMessage);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "startup.start_again");
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Try_again_opens_her_plan_once_it_can_be_read()
    {
        var vm = await OpenAPlanThePhoneCannotOpenAsync();
        _app.Repository.Unreadable = null;

        await vm.TryAgainCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_share_that_opens_maia_goes_to_results_to_file_it()
    {
        await _app.CreatePlanAsync();
        _app.Shares.Receive([new PickedFile("report.pdf", "application/pdf", new byte[16])]);

        await Create().OpenAsync();

        Assert.Equal(new[] { "results" }, _app.Navigator.Visited);
        Assert.Single(_app.Shares.Pending);
    }

    [Fact]
    public async Task A_share_before_she_has_a_plan_asks_her_to_set_up_first()
    {
        _app.Shares.Receive([new PickedFile("report.pdf", "application/pdf", new byte[16])]);

        await Create().OpenAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Empty(_app.Shares.Pending);
        Assert.Contains(Localization.Text.Get("Share_NoPlan"), _app.Notifier.Shown);
    }

    [Fact]
    public async Task Opening_the_plan_tidies_old_files_it_does_not_point_at()
    {
        await _app.CreatePlanAsync();
        _app.Attachments.Now = () => _app.Clock.UtcNow.AddDays(-1);
        await _app.Attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        var vm = Create();

        await vm.OpenAsync();
        await vm.Tidying;

        Assert.Empty(_app.Attachments.Blobs);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_plan_that_cannot_be_read_leaves_every_file_alone()
    {
        await _app.CreatePlanAsync();
        _app.Attachments.Now = () => _app.Clock.UtcNow.AddDays(-1);
        await _app.Attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        _app.Repository.Unreadable = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));
        var vm = Create();

        await vm.OpenAsync();
        await vm.Tidying;

        Assert.True(vm.CannotOpen);
        Assert.Single(_app.Attachments.Blobs);
    }

    [Fact]
    public void Its_words_are_calm_and_in_greek_too()
    {
        using (new CultureScope("el-GR"))
        {
            Assert.Equal("Η Maia δεν μπορεί να ανοίξει το πλάνο σου", Localization.Text.Get("Startup_CannotOpenTitle"));
            Assert.Equal("Χρειάζεται νεότερη Maia", Localization.Text.Get("Startup_UpdateTitle"));
            Assert.Equal("Το πλάνο σου δεν άνοιξε αυτή τη φορά", Localization.Text.Get("Startup_OpenFailedTitle"));
        }

        Assert.Equal("Maia can't open your journey", Localization.Text.Get("Startup_CannotOpenTitle"));
        Assert.Equal("A newer Maia is needed", Localization.Text.Get("Startup_UpdateTitle"));
        Assert.Equal("Your journey didn't open this time", Localization.Text.Get("Startup_OpenFailedTitle"));
    }

    /// <summary>Opens the plan through <paramref name="inner"/> once <see cref="Failure"/> is cleared.</summary>
    private sealed class FailingRepository(IPlanRepository inner) : IPlanRepository
    {
        public Exception? Failure { get; set; }

        public Task<PregnancyPlan?> LoadAsync(CancellationToken cancellationToken) =>
            Failure is null ? inner.LoadAsync(cancellationToken) : Task.FromException<PregnancyPlan?>(Failure);

        public Task SaveAsync(PregnancyPlan plan, CancellationToken cancellationToken) => inner.SaveAsync(plan, cancellationToken);

        public Task DeleteAsync(CancellationToken cancellationToken) => inner.DeleteAsync(cancellationToken);

        public bool LastLoadUsedPreviousCopy => inner.LastLoadUsedPreviousCopy;
    }

    /// <summary>Holds every load until <see cref="Open"/>, as a slow phone does; counts the loads.</summary>
    private sealed class SlowRepository(IPlanRepository inner) : IPlanRepository
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Loads { get; private set; }

        public void Open() => _gate.TrySetResult();

        public async Task<PregnancyPlan?> LoadAsync(CancellationToken cancellationToken)
        {
            Loads++;
            await _gate.Task;
            return await inner.LoadAsync(cancellationToken);
        }

        public Task SaveAsync(PregnancyPlan plan, CancellationToken cancellationToken) => inner.SaveAsync(plan, cancellationToken);

        public Task DeleteAsync(CancellationToken cancellationToken) => inner.DeleteAsync(cancellationToken);

        public bool LastLoadUsedPreviousCopy => inner.LastLoadUsedPreviousCopy;
    }
}
