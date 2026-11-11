using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Settings;

/// <summary>What is shared as diagnostics: a line about Maia and the phone, then one line per problem.</summary>
/// <param name="Header">"Maia 1.0 (12) · Android 14".</param>
/// <param name="Entries">The problem log's lines (see <see cref="ProblemReport"/>), oldest first.</param>
public sealed record DiagnosticsReport(string Header, IReadOnlyList<string> Entries);

/// <summary>The problem log on this phone. Implemented by the app.</summary>
public interface IDiagnostics
{
    /// <summary>The report as it stands: the header line and every line the log holds.</summary>
    Task<DiagnosticsReport> ReadAsync();

    /// <summary>Writes <paramref name="report"/> into a private cache file and opens the phone's share sheet for it.</summary>
    Task ShareAsync(DiagnosticsReport report);
}

/// <summary>
/// "Share diagnostics" in Settings. When something goes wrong Maia notes an operation code and an error type on this
/// phone, never anything from her plan (<see cref="ProblemReport"/>). Nothing leaves the phone on its own: only her tap
/// on "Share diagnostics" hands the file (the codes, when they happened, Maia's version and the Android version) to the
/// phone's share sheet, once, and she chooses where it goes. With nothing noted, a toast says there is nothing to
/// share. "Delete everything" removes the log.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IDiagnostics _diagnostics;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    public DiagnosticsViewModel(IDiagnostics diagnostics, INotifier notifier, IErrorReporter errors)
    {
        _diagnostics = diagnostics;
        _notifier = notifier;
        _errors = errors;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShareCommand))]
    public partial bool IsBusy { get; private set; }

    private bool CanShare() => !IsBusy;

    /// <summary>
    /// Reads the log and hands it straight to the share sheet; with nothing noted, says so in a toast instead. A log
    /// that can't be read or a share sheet that fails is said calmly and reported by code.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShare))]
    private async Task ShareAsync()
    {
        IsBusy = true;
        try
        {
            DiagnosticsReport? report = null;
            var error = await PlanChange.RunAsync(async () => report = await _diagnostics.ReadAsync(), "settings.diagnostics_read", _errors);
            if (error is not null || report is null)
            {
                _notifier.ShowNotice(error ?? Text.Get("Common_Error"));
                return;
            }

            if (report.Entries.Count == 0)
            {
                _notifier.ShowNotice(Text.Get("Diagnostics_Nothing"));
                return;
            }

            if (await PlanChange.RunAsync(() => _diagnostics.ShareAsync(report), "settings.diagnostics_share", _errors) is { } failed)
            {
                _notifier.ShowNotice(failed);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
