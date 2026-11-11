using System.Collections.ObjectModel;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Domain.Plans;

/// <summary>
/// The parent's pregnancy plan: dating, checks with their bookings, results, tasks and care team.
/// This is the only aggregate in v1; every change goes through it so rules and events stay in one place.
/// A change that would not change anything is a no-op: no event, no new revision.
/// </summary>
public sealed class PregnancyPlan : AggregateRoot
{
    public const int MaxChecks = 100;
    public const int MaxTasks = 500;
    public const int MaxQuestions = 200;
    public const int MaxReadings = 2000;
    public const int MaxTimeZoneIdLength = 64;

    private readonly List<PlannedCheck> _checks;
    private readonly List<PlanTask> _tasks;
    private readonly List<DoctorQuestion> _questions;
    private readonly List<ResultAttachment> _results;
    private readonly List<BloodPressureReading> _readings;
    private readonly ReadOnlyCollection<PlannedCheck> _checksView;
    private readonly ReadOnlyCollection<PlanTask> _tasksView;
    private readonly ReadOnlyCollection<DoctorQuestion> _questionsView;
    private readonly ReadOnlyCollection<ResultAttachment> _resultsView;
    private readonly ReadOnlyCollection<BloodPressureReading> _readingsView;

    private PregnancyPlan(
        PlanId id,
        PregnancyDating dating,
        TemplateReference template,
        string language,
        DateTimeOffset createdAt,
        IEnumerable<PlannedCheck> checks,
        IEnumerable<PlanTask> tasks,
        CareTeam careTeam,
        long revision,
        IEnumerable<DoctorQuestion>? questions = null,
        IEnumerable<ResultAttachment>? results = null,
        string? timeZoneId = null,
        IEnumerable<BloodPressureReading>? readings = null)
    {
        Id = id;
        Dating = dating;
        Template = template;
        Language = language;
        CreatedAt = createdAt;
        _checks = checks.ToList();
        _tasks = tasks.ToList();
        _questions = questions?.ToList() ?? [];
        _results = results?.ToList() ?? [];
        _readings = readings?.ToList() ?? [];
        _checksView = _checks.AsReadOnly();
        _tasksView = _tasks.AsReadOnly();
        _questionsView = _questions.AsReadOnly();
        _resultsView = _results.AsReadOnly();
        _readingsView = _readings.AsReadOnly();
        TimeZoneId = timeZoneId;
        CareTeam = careTeam;
        Revision = revision;
        SortChecks();
        SortReadings();
    }

    public PlanId Id { get; }

    public PregnancyDating Dating { get; private set; }

    public TemplateReference Template { get; private set; }

    /// <summary>Two-letter language code the parent chose; content falls back to English.</summary>
    public string Language { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Her home time zone (an IANA id such as <c>Europe/Athens</c>): every booking, visit and task time is a wall-clock
    /// time there, so a trip abroad never moves her appointments. Null only for a plan saved before Maia kept one, until
    /// Maia sets it to the phone's zone (<see cref="SetHomeTimeZoneIfUnknown"/>).
    /// </summary>
    public string? TimeZoneId { get; private set; }

    /// <summary>Every entry on her journey, ordered by window (one with only a trimester first in it).</summary>
    public IReadOnlyList<PlannedCheck> Checks => _checksView;

    public IReadOnlyList<PlanTask> Tasks => _tasksView;

    /// <summary>Her questions, in the order she keeps them: the newest first, unless she moved them.</summary>
    public IReadOnlyList<DoctorQuestion> Questions => _questionsView;

    /// <summary>Her results: what she keeps on Results. They stand on their own, apart from the entries on her journey.</summary>
    public IReadOnlyList<ResultAttachment> Results => _resultsView;

    /// <summary>The blood pressure readings she logged, by the day and time each was taken, the earliest first.</summary>
    public IReadOnlyList<BloodPressureReading> BloodPressure => _readingsView;

    public CareTeam CareTeam { get; }

    /// <summary>
    /// Creates a plan from a template, with only the checks she kept (and the due date): one she switched off is not
    /// created at all. Given <paramref name="today"/>, a new plan starts with its past empty: a check whose weeks have
    /// passed by then is left out (she can add what she did herself), and the due date stays.
    /// </summary>
    /// Missing entries in <paramref name="includeOverrides"/> use the template default.
    public static PregnancyPlan Create(
        PlanId id,
        PregnancyDating dating,
        PlanTemplate template,
        string language,
        DateTimeOffset createdAt,
        IReadOnlyDictionary<CheckId, bool>? includeOverrides = null,
        string? timeZoneId = null,
        DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(dating);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (timeZoneId is not null)
        {
            EnsureValidTimeZone(timeZoneId);
        }

        foreach (var overridden in includeOverrides?.Keys ?? Enumerable.Empty<CheckId>())
        {
            if (template.Find(overridden) is null)
            {
                throw new DomainException("plan.override.unknown_check", "A setup choice names a check the template does not have.");
            }
        }

        var age = today is { } day ? dating.On(day) : (GestationalAge?)null;
        var checks = template.Checks
            .Where(t => (age is not { } now || t.Category == CheckCategory.Milestone || !t.Window.HasPassed(now)) && Kept(t, includeOverrides))
            .Select(FromTemplate);

        return new PregnancyPlan(
            id,
            dating,
            new TemplateReference(template.Id, template.Version),
            language.ToLowerInvariant(),
            createdAt,
            checks,
            [],
            new CareTeam(),
            revision: 0,
            timeZoneId: timeZoneId);
    }

    /// <summary>
    /// Rebuilds a stored plan without re-running creation rules: text limits are for what she types now, so a later
    /// version can tighten one without making saved plans unreadable. Persistence only.
    /// <para>
    /// Rules that link fields together are checked, because code relies on them: a booked check has its appointment, a
    /// milestone is never booked or done, and an appointment names only contacts in the care team. A record that breaks
    /// one is repaired the safe way (the check goes back to needing a booking, the unknown contact is forgotten), so
    /// one bad record never makes the whole plan unreadable. <paramref name="repairs"/> gets a code for each repair.
    /// </para>
    /// </summary>
    public static PregnancyPlan Rehydrate(
        PlanId id,
        PregnancyDating dating,
        TemplateReference template,
        string language,
        DateTimeOffset createdAt,
        IEnumerable<PlannedCheck> checks,
        IEnumerable<PlanTask> tasks,
        CareTeam careTeam,
        long revision,
        IEnumerable<DoctorQuestion>? questions = null,
        IEnumerable<ResultAttachment>? results = null,
        ICollection<string>? repairs = null,
        string? timeZoneId = null,
        IEnumerable<BloodPressureReading>? readings = null)
    {
        if (timeZoneId is not null && !IsValidTimeZone(timeZoneId))
        {
            // Unknown instead: Maia sets it to the phone's zone again.
            repairs?.Add("plan.time_zone_invalid");
            timeZoneId = null;
        }

        var plan = new PregnancyPlan(id, dating, template, language, createdAt, checks, tasks, careTeam, revision, questions, results, timeZoneId, readings);
        plan.RepairStoredState(repairs);
        return plan;
    }

    /// <summary>Rebuilds a stored question, whatever limit it was saved under. Persistence only.</summary>
    public static DoctorQuestion RehydrateQuestion(QuestionId id, string text, bool isAnswered) => DoctorQuestion.Restore(id, text, isAnswered);

    /// <summary>Rebuilds a stored blood pressure reading as it was saved. Persistence only.</summary>
    public static BloodPressureReading RehydrateReading(ReadingId id, int systolic, int diastolic, DateOnly date, TimeOnly time) =>
        BloodPressureReading.Restore(id, systolic, diastolic, date, time);

    /// <summary>Rebuilds a stored check. Persistence only.</summary>
    public static PlannedCheck RehydrateCheck(
        CheckId id,
        CheckOrigin origin,
        LocalizedText title,
        LocalizedText? description,
        CheckCategory category,
        WeekWindow window,
        CheckState state,
        Appointment? appointment,
        Trimester? trimesterOnly = null,
        EntryKind? kind = null,
        bool isEdited = false) =>
        new(id, origin, title, description, category, window, state, appointment, trimesterOnly, kind, isEdited);

    /// <summary>
    /// Rebuilds a stored task, whatever limit its title was saved under, and with none of a date, a trimester or a week
    /// when it was saved before tasks needed one. A time needs a date: a stored time without one is dropped (and noted
    /// in <paramref name="repairs"/>) so the task itself is kept; so is a trimester or week that can't stand with the
    /// rest (with a date, or both together). Persistence only.
    /// </summary>
    public static PlanTask RehydrateTask(
        TaskId id,
        string title,
        DateOnly? dueDate,
        TimeOnly? dueTime,
        bool isDone,
        ICollection<string>? repairs = null,
        Trimester? trimester = null,
        int? week = null)
    {
        if (dueTime is not null && dueDate is null)
        {
            repairs?.Add("task.time_without_date");
            dueTime = null;
        }

        if (!PlanTask.IsValidPeriod(dueDate, trimester, week))
        {
            repairs?.Add("task.period_invalid");
            trimester = null;
            week = null;
        }

        return PlanTask.Restore(id, title, dueDate, dueTime, isDone, trimester, week);
    }

    // ---- Queries -------------------------------------------------------------------------

    public PlannedCheck GetCheck(CheckId id) =>
        _checks.FirstOrDefault(c => c.Id == id)
        ?? throw new DomainException("check.not_found", "The check is not in the plan.");

    public GestationalAge AgeOn(DateOnly today) => Dating.On(today);

    /// <summary>What <paramref name="check"/> shows at <paramref name="now"/> on <paramref name="today"/>.</summary>
    public CheckStatus StatusOf(PlannedCheck check, DateOnly today, TimeOnly now)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check.StatusAt(AgeOn(today), today, now);
    }

    /// <summary>
    /// The weeks an entry dated <paramref name="date"/> stands in on her journey: the pregnancy week of that day (the
    /// last week Maia shows, for a day after it).
    /// </summary>
    public static WeekWindow WindowOn(PregnancyDating dating, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(dating);
        return WeekWindow.OfWeek(Math.Min(dating.On(date).Weeks, WeekWindow.MaxWeek));
    }

    /// <summary>The earliest booked appointment on or after <paramref name="today"/>, any time that day.</summary>
    public PlannedCheck? NextAppointment(DateOnly today) => NextAppointment(today, TimeOnly.MinValue);

    /// <summary>
    /// The earliest booked appointment still to come at <paramref name="now"/> on <paramref name="today"/>: on a later
    /// day, or today at a time not yet passed. One with only a day stays next all through its day.
    /// </summary>
    public PlannedCheck? NextAppointment(DateOnly today, TimeOnly now) =>
        _checks
            .Where(c => c is { State: CheckState.Booked, Appointment: { } at }
                && (at.Date > today || (at.Date == today && (at.Time is not { } time || time > now))))
            .OrderBy(c => c.Appointment!.Date)
            .ThenBy(c => c.Appointment!.Time ?? TimeOnly.MinValue)
            .FirstOrDefault();

    // ---- Dating & preferences ------------------------------------------------------------

    /// <summary>
    /// Moves the due date, for example after a dating scan. Week windows follow automatically, and a booked entry stays
    /// at its day, in whichever week it now falls.
    /// </summary>
    public void ChangeDueDate(PregnancyDating dating)
    {
        ArgumentNullException.ThrowIfNull(dating);
        if (dating.DueDate == Dating.DueDate)
        {
            return;
        }

        var previous = Dating.DueDate;
        Dating = dating;
        var moved = false;
        foreach (var check in _checks)
        {
            if (check.Appointment is { } booked && WindowOn(Dating, booked.Date) is var window && window != check.Window)
            {
                check.MoveTo(window);
                moved = true;
            }
        }

        if (moved)
        {
            SortChecks();
        }

        Raise(new DueDateChanged(previous, dating.DueDate));
    }

    /// <summary>
    /// Gives a plan saved before Maia kept a home time zone the one it is used in now (the phone's). A plan that has one
    /// keeps it: her bookings are wall-clock times there, wherever the phone is.
    /// </summary>
    /// <returns>Whether the plan took it.</returns>
    public bool SetHomeTimeZoneIfUnknown(string timeZoneId)
    {
        EnsureValidTimeZone(timeZoneId);
        if (TimeZoneId is not null)
        {
            return false;
        }

        TimeZoneId = timeZoneId;
        Touch();
        return true;
    }

    public void ChangeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var normalized = language.ToLowerInvariant();
        if (normalized == Language)
        {
            return;
        }

        Language = normalized;
        Raise(new PlanLanguageChanged(normalized));
    }

    /// <summary>
    /// Brings a plan made from an older version of its template up to <paramref name="template"/> (ADR 0003):
    /// <list type="bullet">
    /// <item>A check from the template she has not acted on (not booked, not done, not renamed) takes the new wording,
    /// category and weeks (see <see cref="PlannedCheck.TakeTemplateContent"/>).</item>
    /// <item>A check the new version adds is not added: nothing appears on her journey without her. A milestone it adds
    /// (a date, not an exam) shows on her timeline.</item>
    /// <item>Her own entries, checks she acted on, and a check the new version drops stay as they are.</item>
    /// </list>
    /// A template that is not a newer version of the plan's own changes nothing.
    /// </summary>
    /// <returns>Whether her plan reads differently: a check reworded or moved, or a milestone added.</returns>
    public bool UpdateFromTemplate(PlanTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Id != Template.Id || template.Version <= Template.Version)
        {
            return false;
        }

        var changed = false;
        foreach (var check in _checks.Where(c => c.Origin == CheckOrigin.Template))
        {
            if (template.Find(check.Id) is { } newer)
            {
                changed |= check.TakeTemplateContent(newer);
            }
        }

        foreach (var added in template.Checks.Where(t => t.Category == CheckCategory.Milestone && _checks.TrueForAll(c => c.Id != t.Id)).ToList())
        {
            _checks.Add(FromTemplate(added));
            changed = true;
        }

        var previous = Template.Version;
        Template = new TemplateReference(template.Id, template.Version);
        SortChecks();
        Raise(new PlanTemplateUpdated(previous, template.Version));
        return changed;
    }

    // ---- Checks --------------------------------------------------------------------------

    /// <summary>
    /// Books an entry for a day and time, or moves its booking: from not booked, booked or done. A day that has passed
    /// makes it done, without her. It stands at the week of that day. Saving the same booking again changes nothing.
    /// </summary>
    public void Book(CheckId id, Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        Reschedule(id, EntryWhen.Booked(appointment));
    }

    /// <summary>
    /// Gives an entry new timing: booked for a day and time (it gets its reminders), or not booked or done in a
    /// trimester or a week. Saving the same timing again changes nothing.
    /// </summary>
    public void Reschedule(CheckId id, EntryWhen when)
    {
        ArgumentNullException.ThrowIfNull(when);
        EnsureKnownContact(when);
        var check = GetCheck(id);
        var wasBooked = check.State == CheckState.Booked;
        if (!check.Take(when, WindowOf(when)))
        {
            // Nothing moved, so its reminders stay as they are.
            return;
        }

        SortChecks();
        if (when.Appointment is { } appointment)
        {
            Raise(new CheckBooked(id, appointment));
        }
        else if (wasBooked)
        {
            Raise(new CheckBookingCleared(id));
        }
        else
        {
            Touch();
        }
    }

    /// <summary>Gives an entry a new name. The same name again changes nothing.</summary>
    public void Rename(CheckId id, LocalizedText title)
    {
        EnsureValidTitle(title);
        if (GetCheck(id).Rename(title))
        {
            // Its reminders carry its name.
            Raise(new CheckRenamed(id));
        }
    }

    /// <summary>Gives an entry a new note, or none (null). The same note again changes nothing.</summary>
    public void Describe(CheckId id, LocalizedText? note)
    {
        EnsureValidNote(note);
        if (GetCheck(id).Describe(note))
        {
            Touch();
        }
    }

    /// <summary>Deletes an entry from her journey for good, with its booking and reminders. The due date stays.</summary>
    public void Delete(CheckId id)
    {
        var check = GetCheck(id);
        if (check.IsMilestone)
        {
            throw new DomainException("check.is_milestone", "A milestone is not an exam.");
        }

        _checks.Remove(check);
        Raise(new CheckDeleted(id));
    }

    /// <summary>
    /// Adds an entry she made on her journey: a doctor visit, an exam or test, a hospital visit or something else,
    /// with a name she typed or picked. Booked, it gets its reminders as any booking does (<see cref="CheckBooked"/>),
    /// and is done once its time has passed; otherwise it is not booked yet, or done, in a trimester or a week.
    /// </summary>
    /// <param name="title">Its name, in every language it has one in (free text she typed has only one).</param>
    /// <param name="kind">What it is.</param>
    /// <param name="when">When it is.</param>
    /// <param name="note">Her note for it, shown when its card is opened; none when null.</param>
    public CheckId AddEntry(LocalizedText title, EntryKind kind, EntryWhen when, LocalizedText? note = null)
    {
        EnsureValidTitle(title);
        EnsureValidNote(note);
        ArgumentNullException.ThrowIfNull(when);
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("entry.kind.invalid", "Not a kind of entry.");
        }

        EnsureKnownContact(when);
        if (_checks.Count >= MaxChecks)
        {
            throw new DomainException("plan.checks.full", $"A plan can have at most {MaxChecks} checks.");
        }

        var entry = new PlannedCheck(
            CheckId.NewCustom(),
            CheckOrigin.AddedByParent,
            title,
            note,
            CheckCategory.IfAdvised,
            WindowOf(when),
            CheckState.NotBooked,
            appointment: null,
            kind: kind);
        entry.Take(when, WindowOf(when));
        _checks.Add(entry);
        SortChecks();
        if (when.Appointment is { } appointment)
        {
            Raise(new CheckBooked(entry.Id, appointment));
        }
        else
        {
            Touch();
        }

        return entry.Id;
    }

    /// <summary>
    /// Setup finished again on a plan it had already made (a step after creating it failed, and she went back and
    /// changed something): a check she switched on that her journey does not have joins it from
    /// <paramref name="template"/> (unless its weeks have passed by <paramref name="today"/>), and one she switched off
    /// leaves it, unless she has acted on it.
    /// </summary>
    public void ApplySetupChoices(PlanTemplate template, IReadOnlyDictionary<CheckId, bool> choices, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(choices);
        var age = AgeOn(today);
        var changed = false;
        foreach (var (id, include) in choices)
        {
            if (template.Find(id) is not { } offered || offered.Category == CheckCategory.Milestone)
            {
                continue;
            }

            var check = _checks.FirstOrDefault(c => c.Id == id);
            if (include && check is null && !offered.Window.HasPassed(age))
            {
                _checks.Add(FromTemplate(offered));
                changed = true;
            }
            else if (!include && check is { State: CheckState.NotBooked, Origin: CheckOrigin.Template, IsEdited: false })
            {
                _checks.Remove(check);
                changed = true;
            }
        }

        if (changed)
        {
            SortChecks();
            Touch();
        }
    }

    // ---- Results -------------------------------------------------------------------------

    /// <summary>Keeps a result she added on Results.</summary>
    public void AttachResult(ResultAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        EnsureNewResult(attachment);
        _results.Add(attachment);
        Touch();
    }

    /// <summary>Removes a result. Its stored file goes with it (<see cref="ResultRemoved"/>).</summary>
    public void RemoveResult(AttachmentId attachmentId)
    {
        var result = FindResult(attachmentId);
        _results.Remove(result);
        Raise(new ResultRemoved(attachmentId, result.BlobId));
    }

    /// <summary>Gives a result a new name; the file itself is untouched. Saving the same name changes nothing.</summary>
    /// <returns>Whether the name changed.</returns>
    public bool RenameResult(AttachmentId attachmentId, string displayName)
    {
        var result = FindResult(attachmentId);
        var renamed = result.Renamed(displayName);
        if (renamed.DisplayName == result.DisplayName)
        {
            return false;
        }

        _results[_results.IndexOf(result)] = renamed;
        Touch();
        return true;
    }

    /// <summary>A result in the plan.</summary>
    public ResultAttachment FindResult(AttachmentId attachmentId) =>
        _results.FirstOrDefault(r => r.Id == attachmentId)
            ?? throw new DomainException("attachment.not_found", "The result is not in the plan.");

    // ---- Tasks ---------------------------------------------------------------------------

    /// <summary>
    /// Adds a task for a date (and maybe a time), or, with no date, for a <paramref name="trimester"/> or a pregnancy
    /// <paramref name="week"/>: exactly one of the three. One with a date and a time gets its reminder.
    /// </summary>
    public TaskId AddTask(string title, DateOnly? dueDate = null, TimeOnly? dueTime = null, Trimester? trimester = null, int? week = null)
    {
        if (_tasks.Count >= MaxTasks)
        {
            throw new DomainException("plan.tasks.full", $"A plan can have at most {MaxTasks} tasks.");
        }

        var task = new PlanTask(TaskId.New(), title, dueDate, dueTime, isDone: false, trimester, week);
        _tasks.Add(task);
        if (task.IsScheduled)
        {
            Raise(new TaskScheduled(task.Id));
        }
        else
        {
            Touch();
        }

        return task.Id;
    }

    public void SetTaskDone(TaskId id, bool done)
    {
        var task = GetTask(id);
        if (task.IsDone == done)
        {
            return;
        }

        // Ticking a task off unschedules it if it was scheduled; unticking schedules it again if it has a day and time.
        var wasScheduled = task.IsScheduled;
        task.SetDone(done);
        if (wasScheduled)
        {
            Raise(new TaskUnscheduled(id));
        }
        else if (task.IsScheduled)
        {
            Raise(new TaskScheduled(id));
        }
        else
        {
            Touch();
        }
    }

    /// <summary>
    /// Changes a task's title and when it is (a date and maybe a time, or a trimester or a week, as for a new task). A
    /// task that ends up scheduled gets its reminder; one that loses its date or time loses it. Saving it unchanged
    /// changes nothing.
    /// </summary>
    public void EditTask(TaskId id, string title, DateOnly? dueDate = null, TimeOnly? dueTime = null, Trimester? trimester = null, int? week = null)
    {
        var task = GetTask(id);
        var wasScheduled = task.IsScheduled;
        if (!task.Change(title, dueDate, dueTime, trimester, week))
        {
            return;
        }

        if (task.IsScheduled)
        {
            Raise(new TaskScheduled(id));
        }
        else if (wasScheduled)
        {
            Raise(new TaskUnscheduled(id));
        }
        else
        {
            Touch();
        }
    }

    public void RemoveTask(TaskId id) => Remove(GetTask(id));

    /// <summary>
    /// Clears the tasks she ticked off, keeping the ones still to do. Each leaves as <see cref="RemoveTask"/> takes a
    /// task away, so the reminders on the phone follow it the same way (a ticked task lost its reminder
    /// when she ticked it, so there is nothing more to take out). A plan with nothing ticked is left unchanged.
    /// </summary>
    /// <returns>How many were cleared.</returns>
    public int RemoveDoneTasks()
    {
        var done = _tasks.Where(t => t.IsDone).ToList();
        foreach (var task in done)
        {
            Remove(task);
        }

        return done.Count;
    }

    public PlanTask GetTask(TaskId id) =>
        _tasks.FirstOrDefault(t => t.Id == id)
        ?? throw new DomainException("task.not_found", $"Task '{id}' is not in the plan.");

    // ---- Questions ------------------------------------------------------------------------

    /// <summary>Adds a question she wrote, at the top of her list, above every other.</summary>
    public QuestionId AddQuestion(string text)
    {
        if (_questions.Count >= MaxQuestions)
        {
            throw new DomainException("plan.questions.full", $"A plan can have at most {MaxQuestions} questions.");
        }

        var question = new DoctorQuestion(QuestionId.New(), text, isAnswered: false);
        _questions.Insert(0, question);
        Touch();
        return question.Id;
    }

    public void SetQuestionAnswered(QuestionId id, bool answered)
    {
        var question = GetQuestion(id);
        if (question.IsAnswered == answered)
        {
            return;
        }

        question.SetAnswered(answered);
        Touch();
    }

    /// <summary>Rewords a question. Saving it unchanged changes nothing.</summary>
    public void EditQuestion(QuestionId id, string text)
    {
        if (GetQuestion(id).ChangeText(text))
        {
            Touch();
        }
    }

    public void RemoveQuestion(QuestionId id)
    {
        _questions.Remove(GetQuestion(id));
        Touch();
    }

    /// <summary>
    /// Moves a question to <paramref name="index"/> in her list (clamped to the list), the others keeping their order.
    /// Moving it to where it already is changes nothing.
    /// </summary>
    public void MoveQuestion(QuestionId id, int index)
    {
        var question = GetQuestion(id);
        var from = _questions.IndexOf(question);
        var to = Math.Clamp(index, 0, _questions.Count - 1);
        if (from == to)
        {
            return;
        }

        _questions.RemoveAt(from);
        _questions.Insert(to, question);
        Touch();
    }

    /// <summary>Clears the questions she ticked off as answered, keeping the ones still to ask.</summary>
    /// <returns>How many were cleared.</returns>
    public int RemoveAnsweredQuestions()
    {
        var removed = _questions.RemoveAll(q => q.IsAnswered);
        if (removed > 0)
        {
            Touch();
        }

        return removed;
    }

    public DoctorQuestion GetQuestion(QuestionId id) =>
        _questions.FirstOrDefault(q => q.Id == id)
        ?? throw new DomainException("question.not_found", $"Question '{id}' is not in the plan.");

    // ---- Blood pressure --------------------------------------------------------------------

    /// <summary>
    /// Logs a blood pressure reading she took: the upper and lower numbers as her monitor shows them (see
    /// <see cref="BloodPressureReading.IsValid"/>), at the day and time she took it.
    /// </summary>
    public ReadingId LogBloodPressure(int systolic, int diastolic, DateOnly date, TimeOnly time)
    {
        if (_readings.Count >= MaxReadings)
        {
            throw new DomainException("plan.readings.full", $"A plan can have at most {MaxReadings} readings.");
        }

        var reading = BloodPressureReading.Create(ReadingId.New(), systolic, diastolic, date, time);
        _readings.Add(reading);
        SortReadings();
        Touch();
        return reading.Id;
    }

    /// <summary>Deletes a reading for good. Readings are never changed: one logged wrongly is deleted and logged again.</summary>
    public void RemoveBloodPressure(ReadingId id)
    {
        var reading = _readings.FirstOrDefault(r => r.Id == id)
            ?? throw new DomainException("reading.not_found", $"Reading '{id}' is not in the plan.");
        _readings.Remove(reading);
        Touch();
    }

    // ---- Care team -----------------------------------------------------------------------

    /// <summary>Links a phone contact. Linking the same number to the same role twice is a no-op.</summary>
    public bool LinkContact(CareContact contact)
    {
        var added = CareTeam.Link(contact);
        if (added)
        {
            // Who an appointment is with may read differently on screen.
            Raise(new CareTeamChanged());
        }

        return added;
    }

    /// <summary>Unlinks a contact and removes it from any appointment that referenced it.</summary>
    public void UnlinkContact(CareContactId id)
    {
        CareTeam.Unlink(id);
        foreach (var check in _checks)
        {
            check.ForgetContact(id);
        }

        Raise(new CareTeamChanged());
    }

    /// <summary>Repairs what a stored plan may carry against the rules that link its fields (see <see cref="Rehydrate"/>).</summary>
    private void RepairStoredState(ICollection<string>? repairs)
    {
        foreach (var check in _checks)
        {
            if (check.RepairStoredState() is { } repaired)
            {
                repairs?.Add(repaired);
            }

            if (check.Appointment?.With is { } contactId && CareTeam.Find(contactId) is null)
            {
                check.ForgetContact(contactId);
                repairs?.Add("appointment.contact_unknown");
            }

            // A booking's day sets its week (a plan saved before that kept a check's usual weeks).
            if (check.Appointment is { } booked && WindowOn(Dating, booked.Date) is var window && window != check.Window)
            {
                check.MoveTo(window);
            }
        }

        SortChecks();
    }

    /// <summary>
    /// A time zone id as the phone gives it: a short plain-ASCII name such as <c>Europe/Athens</c> (or, on Windows, one
    /// like <c>GTB Standard Time</c>), never free text.
    /// </summary>
    private static bool IsValidTimeZone(string timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && timeZoneId.Length <= MaxTimeZoneIdLength
        && timeZoneId.Trim().Length == timeZoneId.Length
        && timeZoneId.All(c => c is >= ' ' and < '\u007f');

    private static void EnsureValidTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (!IsValidTimeZone(timeZoneId))
        {
            throw new DomainException("plan.time_zone.invalid", "The time zone id is not valid.");
        }
    }

    /// <summary>A check she kept at setup, not booked, at its usual weeks.</summary>
    private static PlannedCheck FromTemplate(CheckTemplate template) =>
        new(template.Id, CheckOrigin.Template, template.Title, template.Description, template.Category, template.Window, CheckState.NotBooked, appointment: null);

    /// <summary>Whether setup puts a template check on her journey: she kept it (or left it as it comes), or it is a milestone.</summary>
    private static bool Kept(CheckTemplate template, IReadOnlyDictionary<CheckId, bool>? includeOverrides) =>
        template.Category == CheckCategory.Milestone
        || (includeOverrides is not null && includeOverrides.TryGetValue(template.Id, out var choice) ? choice : template.IncludedByDefault);

    /// <summary>The weeks an entry stands in for <paramref name="when"/>: its booking's week, the week, or the trimester's weeks.</summary>
    /// <remarks>Every <see cref="EntryWhen"/> has exactly one of the three (its factories make sure).</remarks>
    private WeekWindow WindowOf(EntryWhen when) =>
        when.Appointment is { } booked ? WindowOn(Dating, booked.Date)
        : when.Week is { } week ? WeekWindow.OfWeek(week)
        : WeekWindow.OfTrimester(when.Trimester!.Value);

    private static void EnsureValidTitle(LocalizedText title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (title.Values.Values.Any(t => t.Length > PlannedCheck.MaxTitleLength))
        {
            throw new DomainException("entry.title.too_long", "The entry's name is too long.");
        }
    }

    private static void EnsureValidNote(LocalizedText? note)
    {
        if (note is not null && note.Values.Values.Any(t => t.Length > PlannedCheck.MaxNoteLength))
        {
            throw new DomainException("entry.note.too_long", "The entry's note is too long.");
        }
    }

    private void EnsureKnownContact(EntryWhen when)
    {
        if (when.Appointment?.With is { } contactId && CareTeam.Find(contactId) is null)
        {
            throw new DomainException("appointment.contact_unknown", $"Contact '{contactId}' is not in the care team.");
        }
    }

    /// <summary>A result's id is its own across the whole plan.</summary>
    private void EnsureNewResult(ResultAttachment attachment)
    {
        if (_results.Any(r => r.Id == attachment.Id))
        {
            throw new DomainException("attachment.duplicate", "The result is already in the plan.");
        }
    }

    /// <summary>Takes a task off her list; one with a date and time loses its reminder too.</summary>
    private void Remove(PlanTask task)
    {
        _tasks.Remove(task);
        if (task.IsScheduled)
        {
            Raise(new TaskUnscheduled(task.Id));
        }
        else
        {
            Touch();
        }
    }

    /// <summary>
    /// Stable sort by weeks, so checks with the same weeks keep their template order; one with only a trimester stands
    /// first in it.
    /// </summary>
    private void SortChecks()
    {
        var sorted = _checks.OrderBy(c => c.Window.FromWeek).ThenBy(c => c.TrimesterOnly is null).ThenBy(c => c.Window.ToWeek).ToList();
        _checks.Clear();
        _checks.AddRange(sorted);
    }

    /// <summary>Stable sort by the day and time each reading was taken, so two at the same minute keep the order logged.</summary>
    private void SortReadings()
    {
        var sorted = _readings.OrderBy(r => r.Date).ThenBy(r => r.Time).ToList();
        _readings.Clear();
        _readings.AddRange(sorted);
    }
}

/// <summary>Which template (and version) a plan was created from.</summary>
public sealed record TemplateReference(string Id, int Version);
