using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Setup;

/// <summary>Setup step 2: the suggested plan, with a switch per check. The plan is created at the end of step 3.</summary>
public sealed partial class SetupPlanViewModel : ObservableObject
{
    private readonly IPlanTemplateCatalog _templates;
    private readonly SetupSession _session;
    private readonly IClock _clock;
    private readonly INavigator _navigator;

    public SetupPlanViewModel(IPlanTemplateCatalog templates, SetupSession session, IClock clock, INavigator navigator)
    {
        _templates = templates;
        _session = session;
        _clock = clock;
        _navigator = navigator;
    }

    public ObservableCollection<CheckGroup> Groups { get; } = [];

    /// <summary>"Common checks for a pregnancy due Sun 17 Jan. Your doctor may suggest a different set…"</summary>
    [ObservableProperty]
    public partial string Intro { get; private set; } = string.Empty;

    /// <summary>
    /// Why no plan can be suggested ("This plan is waiting for clinical review…"), or null. Only a build that refuses
    /// Draft templates gets here (ADR 0003).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBlocked), nameof(Title))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>"Used Maia on another phone? You can bring your plan here with a backup." Shown under <see cref="ErrorMessage"/>.</summary>
    [ObservableProperty]
    public partial string? ErrorHelp { get; private set; }

    /// <summary>No plan can be suggested: the intro, count and "Looks good" give way to the explanation and "Restore a backup".</summary>
    public bool IsBlocked => ErrorMessage is not null;

    /// <summary>"Here's a typical plan", or, when none can be suggested, "The typical plan isn't ready yet".</summary>
    public string Title => Text.Get(IsBlocked ? "SetupPlan_BlockedTitle" : "SetupPlan_Title");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial bool IsReady { get; private set; }

    public async Task LoadAsync()
    {
        if (_session.DueDate is not { } due)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        PlanTemplate template;
        try
        {
            template = await _templates.GetAsync(AppConfig.DefaultCountry, CancellationToken.None);
        }
        catch (DomainException ex) when (ex.Code == "template.not_reviewed")
        {
            ErrorMessage = Text.Get("SetupPlan_NotReviewed");
            ErrorHelp = Text.Get("SetupPlan_NotReviewedHelp");
            return;
        }

        var dating = PregnancyDating.FromDueDate(due);
        var age = dating.On(_clock.Today);
        var language = Text.Language;
        Intro = Text.Format("SetupPlan_Intro", DisplayFormat.Date(due));

        Groups.Clear();
        // Only what is still to come: her past starts empty (a check whose weeks have passed is not in the new plan), and
        // she can add what she has done herself.
        foreach (var section in template.Checks
                     .Where(c => c.Category != CheckCategory.Milestone && !c.Window.HasPassed(age))
                     .GroupBy(c => PlanQueries.SectionKeyFor(c.Window.FromWeek)))
        {
            var options = section.Select(c =>
            {
                var included = _session.IncludeOverrides.TryGetValue(c.Id, out var choice) ? choice : c.IncludedByDefault;
                var option = new CheckOption(
                    c.Id,
                    c.Title.In(language),
                    DisplayFormat.Window(c.Window, dating),
                    DisplayFormat.Category(c.Category),
                    c.Category.ToString(),
                    included);
                option.PropertyChanged += OnOptionChanged;
                return option;
            });
            Groups.Add(new CheckGroup(DisplayFormat.Section(section.Key), options));
        }

        IsReady = true;
    }

    /// <summary>"Looks good": on to her doctor and reminders.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private async Task ContinueAsync()
    {
        await _session.SaveAsync(SetupStep.Care);
        await _navigator.GoToSetupCareAsync();
    }

    /// <summary>"Restore a backup": the way forward when no plan can be suggested, as on step 1.</summary>
    [RelayCommand]
    private Task RestoreAsync() => _navigator.GoToRestoreAsync();

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Raised only by the options shown, and an option's include switch is the one thing about it that changes.
        var option = (CheckOption)sender!;
        _session.IncludeOverrides[option.Id] = option.IsIncluded;
        _ = _session.SaveAsync(SetupStep.Plan);
    }
}

/// <summary>One check in the suggested plan, with its include switch.</summary>
public sealed partial class CheckOption : ObservableObject
{
    public CheckOption(CheckId id, string title, string window, string tag, string tagKind, bool isIncluded)
    {
        Id = id;
        Title = title;
        Window = window;
        Tag = tag;
        TagKind = tagKind;
        IsIncluded = isIncluded;
    }

    public CheckId Id { get; }

    public string Title { get; }

    public string Window { get; }

    public string Tag { get; }

    /// <summary>Picks the tag colours: "Routine", or another category.</summary>
    public string TagKind { get; }

    [ObservableProperty]
    public partial bool IsIncluded { get; set; }
}

/// <summary>A trimester heading with its checks; a list so grouped collection views can bind to it.</summary>
public sealed class CheckGroup : List<CheckOption>
{
    public CheckGroup(string title, IEnumerable<CheckOption> options)
        : base(options)
    {
        Title = title;
    }

    public string Title { get; }
}
