using Maia.Application.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Results;

/// <summary>
/// Files another app shared with Maia (a lab report from her email, a photo from the gallery), held until Results
/// keeps them in her results. Choosing Maia in the share sheet was her yes, so Results keeps them as soon as it shows.
/// Register as a singleton: the app hands files in, Results keeps them.
/// </summary>
public sealed class ShareIntake
{
    private readonly PlanQueries _queries;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private IReadOnlyList<PickedFile> _pending = [];

    public ShareIntake(PlanQueries queries, INavigator navigator, INotifier notifier)
    {
        _queries = queries;
        _navigator = navigator;
        _notifier = notifier;
    }

    /// <summary>Raised when files arrive, so Results, if it shows, keeps them.</summary>
    public event EventHandler? Changed;

    /// <summary>The files waiting to be kept, in the order they were shared.</summary>
    public IReadOnlyList<PickedFile> Pending => _pending;

    /// <summary>Takes in what was shared; anything shared before and not kept yet is put aside.</summary>
    public void Receive(IReadOnlyList<PickedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Release(_pending);
        _pending = [.. files];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hands the waiting files over to be kept.</summary>
    public IReadOnlyList<PickedFile> Take()
    {
        var files = _pending;
        _pending = [];
        return files;
    }

    /// <summary>Puts the shared files aside without keeping them: there is no plan to keep them in.</summary>
    public void Discard() => Release(Take());

    /// <summary>
    /// Opens the right screen for waiting files: Results, which keeps them. With no plan yet there is nowhere to
    /// keep them: she is told so, calmly, and taken to setup.
    /// </summary>
    /// <returns>Whether files were waiting (and a screen was opened).</returns>
    public async Task<bool> OpenAsync()
    {
        if (_pending.Count == 0)
        {
            return false;
        }

        if (await _queries.GetTimelineAsync() is null)
        {
            Discard();
            await _navigator.GoToSetupAsync();

            // After navigating, so setup shows it rather than the screen that is closing.
            _notifier.ShowNotice(Text.Get("Share_NoPlan"));
            return true;
        }

        await _navigator.GoToResultsAsync();
        return true;
    }

    private static void Release(IEnumerable<PickedFile> files)
    {
        foreach (var file in files)
        {
            file.Cleanup?.Invoke();
        }
    }
}
