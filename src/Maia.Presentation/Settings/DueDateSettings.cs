using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Setup;

namespace Maia.Presentation.Settings;

/// <summary>
/// "Your pregnancy" in Settings: her due date, which she can change after setup, for example when a scan dates the
/// pregnancy again. It takes a due date or the first day of her last period, as setup does, and opens in place. Every
/// check's weeks move with the new date; bookings stay on the days she booked, so their reminders need no change.
/// </summary>
public sealed partial class DueDateSettings : ObservableObject
{
    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private DateOnly? _dueDate;

    public DueDateSettings(PlanQueries queries, PlanService plans, IClock clock, INotifier notifier, IErrorReporter errors)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _queries = queries;
        _plans = plans;
        _notifier = notifier;
        _errors = errors;
        Entry = new DueDateEntry(clock, clock.Today.AddDays(PregnancyDating.TermDays / 2));
        Entry.PropertyChanged += OnEntryChanged;
    }

    /// <summary>The date she enters, and what it means today.</summary>
    public DueDateEntry Entry { get; }

    /// <summary>"Due Sun 17 Jan · Week 24 + 3".</summary>
    [ObservableProperty]
    public partial string Current { get; private set; } = string.Empty;

    /// <summary>Whether her plan is loaded, so there is a due date to change.</summary>
    public bool HasPlan => _dueDate is not null;

    /// <summary>Whether the date editor is open.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(EditCommand))]
    public partial bool IsBusy { get; private set; }

    public async Task LoadAsync()
    {
        WeekOverview? week = null;
        await PlanChange.RunAsync(async () => week = await _queries.GetWeekAsync(), "settings.due_date.load", _errors);
        _dueDate = week?.DueDate;
        Current = week is null ? string.Empty : Text.Format("Settings_DueDateCurrent", DisplayFormat.Date(week.DueDate), DisplayFormat.WeekLabel(week.Age));
        OnPropertyChanged(nameof(HasPlan));
        EditCommand.NotifyCanExecuteChanged();
    }

    private bool CanEdit() => HasPlan && !IsBusy;

    /// <summary>Opens the editor on her current due date.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (_dueDate is { } due)
        {
            Entry.Reset(due);
            IsEditing = true;
        }
    }

    [RelayCommand]
    private void Cancel() => IsEditing = false;

    private bool CanSave() => IsEditing && !IsBusy && Entry.IsValid && Entry.EffectiveDueDate != _dueDate;

    /// <summary>Moves her due date: every window follows, bookings stay.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var due = Entry.EffectiveDueDate;
        IsBusy = true;
        try
        {
            var error = await PlanChange.RunAsync(() => _plans.ChangeDueDateAsync(due), "settings.due_date", _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
                return;
            }

            IsEditing = false;
            await LoadAsync();
            _notifier.Show(Text.Format("Toast_DueDateChanged", DisplayFormat.Date(due)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DueDateEntry.IsValid) or nameof(DueDateEntry.EffectiveDueDate))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
