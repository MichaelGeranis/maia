using System.Security.Cryptography;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Application.Plans;

/// <summary>
/// The use cases that change the plan. Every command follows the same shape:
/// load → change the aggregate → save → react to its events (reminders, file cleanup).
/// Reactions run after the save and never fail the command; failures go to <see cref="IErrorReporter"/>. Reminders on
/// this phone follow in the background: a command does not wait for them.
/// </summary>
public sealed class PlanService : IDisposable
{
    /// <summary>How old a stored file must be before <see cref="RemoveOrphanedFilesAsync"/> may delete it.</summary>
    public static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromHours(1);

    private readonly IPlanRepository _plans;
    private readonly IPlanTemplateCatalog _templates;
    private readonly IAttachmentStore _attachments;
    private readonly IDataKey _dataKey;
    private readonly IPlanBackupFormat _backups;
    private readonly IClock _clock;
    private readonly IErrorReporter _errors;
    private readonly PhoneReminders? _reminders;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Register as a singleton, with <see cref="PhoneReminders"/>.</summary>
    /// <remarks>Reminders on this phone (optional) are set again after each change, restore and delete.</remarks>
    public PlanService(
        IPlanRepository plans,
        IPlanTemplateCatalog templates,
        IAttachmentStore attachments,
        IDataKey dataKey,
        IPlanBackupFormat backups,
        IClock clock,
        IErrorReporter errors,
        PhoneReminders? reminders = null)
    {
        _plans = plans;
        _templates = templates;
        _attachments = attachments;
        _dataKey = dataKey;
        _backups = backups;
        _clock = clock;
        _errors = errors;
        _reminders = reminders;
    }

    /// <summary>
    /// Creates her plan on a phone without one. It starts with a new data key: result files an earlier plan left behind
    /// (no plan lists them, so she can't reach them) are removed first, so a key lost with them can never block setup.
    /// </summary>
    public async Task<PregnancyPlan> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new PlanAlreadyExistsException();
            }

            var template = await _templates.GetAsync(request.CountryCode, cancellationToken).ConfigureAwait(false);
            var dating = PregnancyDating.ForToday(request.DueDate, _clock.Today);
            var plan = PregnancyPlan.Create(PlanId.New(), dating, template, request.Language, _clock.UtcNow, request.IncludeOverrides, HomeTimeZone.IdOf(_clock.TimeZone), _clock.Today);
            await ClearStoredDataAsync(cancellationToken).ConfigureAwait(false);
            await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
            Rearm();
            return plan;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PregnancyPlan> GetPlanAsync(CancellationToken cancellationToken = default) =>
        await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) ?? throw new PlanNotFoundException();

    /// <summary>
    /// The first time Maia opens a plan saved before it kept a home time zone, the phone's zone becomes the plan's home
    /// and is saved, so her bookings stay at their time at home when she travels later. Call once the plan has opened;
    /// does nothing when it already has one, or there is no plan.
    /// </summary>
    /// <returns>Whether the plan took the phone's zone.</returns>
    public async Task<bool> KeepHomeTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) is not { TimeZoneId: null } plan)
            {
                return false;
            }

            KeepHomeTimeZone(plan);
            await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ChangeDueDateAsync(DateOnly dueDate, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.ChangeDueDate(PregnancyDating.ForToday(dueDate, _clock.Today)), cancellationToken);

    public Task ChangeLanguageAsync(string language, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.ChangeLanguage(language), cancellationToken);

    // ---- Checks --------------------------------------------------------------------------

    /// <summary>Books an entry for a day and time, or moves its booking; a time that has passed makes it done.</summary>
    public Task BookCheckAsync(CheckId checkId, Appointment appointment, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.Book(checkId, appointment), cancellationToken);

    /// <summary>
    /// Edit on Journey: an entry's name, its note and when it is, in one save. <paramref name="title"/> and
    /// <paramref name="note"/> are as she sees them: unchanged, the entry keeps what it has in every language; changed,
    /// it takes her words (a note left empty is none). A null <paramref name="note"/> leaves the note as it is.
    /// </summary>
    public Task EditEntryAsync(CheckId checkId, string title, EntryWhen when, string? note = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(when);
        return MutateAsync(
            plan =>
            {
                var check = plan.GetCheck(checkId);
                var name = title.Trim();
                if (!string.Equals(check.Title.In(plan.Language), name, StringComparison.Ordinal))
                {
                    plan.Rename(checkId, LocalizedText.Plain(name));
                }

                var words = note?.Trim();
                if (words is not null && !string.Equals(check.Description?.In(plan.Language) ?? string.Empty, words, StringComparison.Ordinal))
                {
                    plan.Describe(checkId, NoteOf(words));
                }

                plan.Reschedule(checkId, when);
            },
            cancellationToken);
    }

    /// <summary>Deletes an entry from her journey for good; its reminders go with it.</summary>
    public Task DeleteEntryAsync(CheckId checkId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.Delete(checkId), cancellationToken);

    /// <summary>
    /// Adds an entry she made on her journey (New): what it is, its name as she typed it, when, and her note (none when
    /// empty). Booked, it gets its reminders as any booking does, and is done once its time has passed; otherwise it is
    /// not booked yet, or done, in a trimester or a week.
    /// </summary>
    public Task<CheckId> AddEntryAsync(string title, EntryKind kind, EntryWhen when, string? note = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return AddEntryAsync(LocalizedText.Plain(title), kind, when, note, cancellationToken);
    }

    /// <summary>
    /// As <see cref="AddEntryAsync(string, EntryKind, EntryWhen, string?, CancellationToken)"/>, for a name Maia has in
    /// every language (one of its usual suggestions), so it follows the plan when she switches language.
    /// </summary>
    public async Task<CheckId> AddEntryAsync(LocalizedText title, EntryKind kind, EntryWhen when, string? note = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(when);
        var described = NoteOf(note);
        CheckId added = default;
        await MutateAsync(plan => added = plan.AddEntry(title, kind, when, described), cancellationToken).ConfigureAwait(false);
        return added;
    }

    /// <summary>Her note as the plan keeps it: her words, or none when she wrote nothing.</summary>
    private static LocalizedText? NoteOf(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : LocalizedText.Plain(note.Trim());

    /// <summary>
    /// Setup finished again on a plan it had already made (a step after creating it failed, and she went back and
    /// changed something): the plan takes the due date and the switches she chose, as one save. A check she switched
    /// on joins her journey; one she switched off leaves it, unless she has acted on it.
    /// </summary>
    public async Task ApplySetupAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = await _templates.GetAsync(request.CountryCode, cancellationToken).ConfigureAwait(false);
        await MutateAsync(
            plan =>
            {
                plan.ChangeDueDate(PregnancyDating.ForToday(request.DueDate, _clock.Today));
                plan.ApplySetupChoices(template, request.IncludeOverrides ?? new Dictionary<CheckId, bool>(), _clock.Today);
            },
            cancellationToken).ConfigureAwait(false);
    }

    // ---- Template ------------------------------------------------------------------------

    /// <summary>
    /// When Maia ships a newer version of the template her plan was made from, and this build may use it (clinically
    /// reviewed, or a build that allows drafts), her plan takes what it holds for the checks she has not acted on, and
    /// the milestones it adds (see <see cref="PregnancyPlan.UpdateFromTemplate"/>). Nothing is saved when there is
    /// nothing newer.
    /// </summary>
    /// <returns>Whether her plan reads differently.</returns>
    public async Task<bool> UpdateFromTemplateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IDomainEvent> events;
        bool changed;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (plan is null || await FindTemplateAsync(plan.Template.Id, cancellationToken).ConfigureAwait(false) is not { } template
                || template.Version <= plan.Template.Version)
            {
                return false;
            }

            changed = plan.UpdateFromTemplate(template);
            await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
            events = plan.DequeueEvents();
        }
        finally
        {
            _gate.Release();
        }

        await DispatchAsync(events).ConfigureAwait(false);
        return changed;
    }

    /// <summary>The shipped template with this id, if this build may use it (ADR 0003); else null.</summary>
    private async Task<PlanTemplate?> FindTemplateAsync(string templateId, CancellationToken cancellationToken)
    {
        foreach (var country in _templates.SupportedCountries)
        {
            try
            {
                var template = await _templates.GetAsync(country, cancellationToken).ConfigureAwait(false);
                if (template.Id == templateId)
                {
                    return template;
                }
            }
            catch (DomainException ex) when (ex.Code == "template.not_reviewed")
            {
                // Not for this build: her plan keeps the wording it has.
            }
        }

        return null;
    }

    // ---- Results -------------------------------------------------------------------------

    /// <summary>Encrypts and stores a file, then keeps it in her results.</summary>
    public async Task<AttachmentId> AttachFileAsync(FileToAttach file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!AttachmentPolicy.IsAllowed(file.MediaType))
        {
            throw AttachmentRejectedException.UnsupportedType(file.MediaType);
        }

        if (file.Content.IsEmpty)
        {
            throw AttachmentRejectedException.Empty();
        }

        if (file.Content.Length > AttachmentPolicy.MaxSizeBytes)
        {
            throw AttachmentRejectedException.TooLarge(AttachmentPolicy.MaxSizeBytes);
        }

        var id = AttachmentId.New();
        string? blobId = null;
        try
        {
            await MutateAsync(
                async plan =>
                {
                    blobId = await _attachments.SaveAsync(file.Content, cancellationToken).ConfigureAwait(false);
                    plan.AttachResult(ResultAttachment.ForFile(id, file.FileName, file.MediaType, file.Content.Length, blobId, _clock.UtcNow));
                },
                cancellationToken,
                remindersFollow: false).ConfigureAwait(false);
        }
        catch when (blobId is { } written)
        {
            // Compensate: never leave an orphaned encrypted file behind.
            await TryAsync(() => _attachments.DeleteAsync(written, CancellationToken.None), "attachment.compensate").ConfigureAwait(false);
            throw;
        }

        return id;
    }

    /// <summary>Gives a result a new name; its file stays as it is.</summary>
    public Task RenameResultAsync(AttachmentId attachmentId, string displayName, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.RenameResult(attachmentId, displayName), cancellationToken, remindersFollow: false);

    /// <summary>Removes a result, and then its stored file.</summary>
    public Task RemoveResultAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.RemoveResult(attachmentId), cancellationToken, remindersFollow: false);

    /// <summary>
    /// Deletes stored result files the plan does not point at: left by a crash between saving a file and saving the
    /// plan, by a removal whose clean-up was cut short, or by a restore that was stopped half-way. Only files older
    /// than <see cref="OrphanGracePeriod"/> go, because a file being attached is saved just before the plan points at
    /// it. Call it once the plan has loaded: it deletes nothing when the plan came from the previous copy, and if the
    /// plan can't be read it throws before deleting anything. With no plan at all, every old file goes.
    /// </summary>
    /// <returns>How many files were deleted.</returns>
    public async Task<int> RemoveOrphanedFilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (_plans.LastLoadUsedPreviousCopy)
            {
                return 0;
            }

            var kept = (plan?.Results ?? []).Select(r => r.BlobId).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var cutoff = _clock.UtcNow - OrphanGracePeriod;
            var removed = 0;
            foreach (var blob in await _attachments.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!kept.Contains(blob.BlobId) && blob.SavedAt < cutoff)
                {
                    await _attachments.DeleteAsync(blob.BlobId, cancellationToken).ConfigureAwait(false);
                    removed++;
                }
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Decrypts a stored result file for viewing or sharing.</summary>
    public async Task<byte[]> ReadResultFileAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(cancellationToken).ConfigureAwait(false);
        return await ReadResultFileAsync(plan.FindResult(attachmentId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Decrypts the stored file of a result taken from a plan the caller has just loaded, without loading it again.</summary>
    public Task<byte[]> ReadResultFileAsync(ResultAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return attachment.BlobId is { } blobId
            ? _attachments.ReadAsync(blobId, cancellationToken)
            : throw new DomainException("attachment.not_a_file", "This result has no stored file.");
    }

    // ---- Tasks ---------------------------------------------------------------------------

    /// <summary>Adds a task for a date (and maybe a time), or with no date for a trimester or a week.</summary>
    public async Task<TaskId> AddTaskAsync(
        string title,
        DateOnly? dueDate = null,
        TimeOnly? dueTime = null,
        Trimester? trimester = null,
        int? week = null,
        CancellationToken cancellationToken = default)
    {
        TaskId added = default;
        await MutateAsync(plan => added = plan.AddTask(title, dueDate, dueTime, trimester, week), cancellationToken).ConfigureAwait(false);
        return added;
    }

    public Task SetTaskDoneAsync(TaskId taskId, bool done, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.SetTaskDone(taskId, done), cancellationToken);

    /// <summary>Changes a task's title and when it is (a date and time, a trimester or a week); with no time it has no reminder.</summary>
    public Task EditTaskAsync(
        TaskId taskId,
        string title,
        DateOnly? dueDate = null,
        TimeOnly? dueTime = null,
        Trimester? trimester = null,
        int? week = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.EditTask(taskId, title, dueDate, dueTime, trimester, week), cancellationToken);

    public Task RemoveTaskAsync(TaskId taskId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.RemoveTask(taskId), cancellationToken);

    /// <summary>
    /// Clears the tasks she ticked off (as Your tasks loads). The reminders on this phone follow as they do
    /// when she deletes a task.
    /// </summary>
    /// <returns>How many were cleared.</returns>
    public async Task<int> ClearDoneTasksAsync(CancellationToken cancellationToken = default)
    {
        var removed = 0;
        await MutateAsync(plan => removed = plan.RemoveDoneTasks(), cancellationToken).ConfigureAwait(false);
        return removed;
    }

    // ---- Questions for the doctor ----------------------------------------------------------

    public async Task<QuestionId> AddQuestionAsync(string text, CancellationToken cancellationToken = default)
    {
        QuestionId added = default;
        await MutateAsync(plan => added = plan.AddQuestion(text), cancellationToken, remindersFollow: false).ConfigureAwait(false);
        return added;
    }

    public Task SetQuestionAnsweredAsync(QuestionId questionId, bool answered, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.SetQuestionAnswered(questionId, answered), cancellationToken, remindersFollow: false);

    public Task EditQuestionAsync(QuestionId questionId, string text, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.EditQuestion(questionId, text), cancellationToken, remindersFollow: false);

    /// <summary>Moves a question to <paramref name="index"/> in her list.</summary>
    public Task MoveQuestionAsync(QuestionId questionId, int index, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.MoveQuestion(questionId, index), cancellationToken, remindersFollow: false);

    public Task RemoveQuestionAsync(QuestionId questionId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.RemoveQuestion(questionId), cancellationToken, remindersFollow: false);

    /// <summary>Clears the questions she ticked off as answered.</summary>
    /// <returns>How many were cleared.</returns>
    public async Task<int> ClearAnsweredQuestionsAsync(CancellationToken cancellationToken = default)
    {
        var removed = 0;
        await MutateAsync(plan => removed = plan.RemoveAnsweredQuestions(), cancellationToken, remindersFollow: false).ConfigureAwait(false);
        return removed;
    }

    // ---- Blood pressure --------------------------------------------------------------------

    /// <summary>Logs a blood pressure reading she took, at the day and time she took it (a wall-clock time at home).</summary>
    public async Task<ReadingId> LogBloodPressureAsync(int systolic, int diastolic, DateOnly date, TimeOnly time, CancellationToken cancellationToken = default)
    {
        ReadingId added = default;
        await MutateAsync(plan => added = plan.LogBloodPressure(systolic, diastolic, date, time), cancellationToken, remindersFollow: false).ConfigureAwait(false);
        return added;
    }

    /// <summary>Deletes a reading for good.</summary>
    public Task RemoveBloodPressureAsync(ReadingId readingId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.RemoveBloodPressure(readingId), cancellationToken, remindersFollow: false);

    // ---- Care team -----------------------------------------------------------------------

    /// <summary>Links the contacts picked in one session. Returns how many were new.</summary>
    public async Task<int> LinkContactsAsync(IEnumerable<ContactToLink> contacts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        var list = contacts.ToList();
        var added = 0;
        await MutateAsync(
            plan =>
            {
                foreach (var c in list)
                {
                    if (plan.LinkContact(new CareContact(CareContactId.New(), c.Role, c.DisplayName, c.PhoneNumber, c.Label, c.DeviceContactId)))
                    {
                        added++;
                    }
                }
            },
            cancellationToken,
            remindersFollow: false).ConfigureAwait(false);
        return added;
    }

    public Task UnlinkContactAsync(CareContactId contactId, CancellationToken cancellationToken = default) =>
        MutateAsync(plan => plan.UnlinkContact(contactId), cancellationToken, remindersFollow: false);

    // ---- Backup --------------------------------------------------------------------------

    /// <summary>
    /// Writes her encrypted backup (the plan and every result file) to <paramref name="output"/>. A result whose file
    /// can't be read (gone, or damaged on disk) is left out rather than failing the whole backup: a new phone is when
    /// she needs the backup most.
    /// </summary>
    /// <returns>The results left out, usually none. Their names are for her to see, never for logs.</returns>
    public async Task<IReadOnlyList<ResultAttachment>> ExportBackupAsync(Stream output, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ValidateBackupPassword(password);

        // Under the gate, so no file disappears half-way through.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) ?? throw new PlanNotFoundException();
            var skipped = await _backups.WriteAsync(output, plan, blobId => TryReadFileAsync(blobId, cancellationToken), password, cancellationToken).ConfigureAwait(false);
            return plan.Results.Where(r => r.BlobId is { } id && skipped.Contains(id)).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A stored file for the backup, or null when it is missing or no longer decrypts (reported by code only).</summary>
    private async Task<byte[]?> TryReadFileAsync(string blobId, CancellationToken cancellationToken)
    {
        try
        {
            return await _attachments.ReadAsync(blobId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidDataException or ArgumentException)
        {
            _errors.Report(ex, "backup.file_unreadable");
            return null;
        }
    }

    /// <summary>
    /// Restores a backup on a phone without a plan she can open: a new phone, or one whose plan can't be opened any more
    /// (its key is gone, or both copies are damaged), which the backup then replaces. What was stored before is removed
    /// only once the password has opened the backup, and the restored plan gets a new data key. From there it is all or
    /// nothing: if the backup turns out damaged, the files already written are deleted again. A plan saved by a newer
    /// Maia is never replaced.
    /// </summary>
    /// <exception cref="BackupUnreadableException">Wrong password, not a backup, damaged, or from a newer app.</exception>
    /// <exception cref="PlanAlreadyExistsException">This phone already has a plan it can open.</exception>
    /// <exception cref="PlanUnreadableException">The plan on this phone was saved by a newer Maia.</exception>
    public async Task<PregnancyPlan> RestoreBackupAsync(Stream input, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(password);

        PregnancyPlan plan;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureNoPlanToKeepAsync(cancellationToken).ConfigureAwait(false);

            var cleared = false;
            async Task ClearOnceAsync()
            {
                if (!cleared)
                {
                    await ClearStoredDataAsync(cancellationToken).ConfigureAwait(false);
                    cleared = true;
                }
            }

            var written = new List<string>();
            try
            {
                plan = await _backups.ReadAsync(
                    input,
                    password,
                    async content =>
                    {
                        // A file only arrives once the password has opened the backup.
                        await ClearOnceAsync().ConfigureAwait(false);
                        var blobId = await _attachments.SaveAsync(content, cancellationToken).ConfigureAwait(false);
                        written.Add(blobId);
                        return blobId;
                    },
                    cancellationToken).ConfigureAwait(false);

                // A backup made from an older template (or on a build that shows drafts) takes the wording this build
                // ships, if it has a newer version. Reminders follow below.
                if (await FindTemplateAsync(plan.Template.Id, cancellationToken).ConfigureAwait(false) is { } template)
                {
                    plan.UpdateFromTemplate(template);
                    _ = plan.DequeueEvents();
                }

                await ClearOnceAsync().ConfigureAwait(false);
                KeepHomeTimeZone(plan);
                await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                foreach (var blobId in written)
                {
                    await TryAsync(() => _attachments.DeleteAsync(blobId, CancellationToken.None), "backup.compensate").ConfigureAwait(false);
                }

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }

        // Her bookings get their reminders on this phone if she has turned them on.
        Rearm();
        return plan;
    }

    /// <summary>
    /// The floor under what Settings asks for (<see cref="BackupPasswordRules"/>): a new backup never gets a short password or
    /// one that is guessed first. Restoring takes any password, so backups made under an older rule still open.
    /// </summary>
    private static void ValidateBackupPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (BackupPasswordRules.Check(password) != BackupPasswordIssue.None)
        {
            throw new ArgumentException("A backup password needs at least 10 characters and must not be easy to guess.", nameof(password));
        }
    }

    /// <summary>A restore may only replace a plan this phone can't open, and never one saved by a newer Maia.</summary>
    private async Task EnsureNoPlanToKeepAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new PlanAlreadyExistsException();
            }
        }
        catch (PlanUnreadableException ex) when (!ex.IsFromNewerApp)
        {
            // Its key is gone or both copies are damaged: she chose to put her backup in its place.
        }
    }

    // ---- Her data ------------------------------------------------------------------------

    /// <summary>
    /// Deletes everything Maia keeps about her pregnancy on this phone: every result file, the plan with its backup
    /// copy, and last the key that encrypted them, so no copy left behind can be opened again (her next plan gets a new
    /// key). Every reminder on the phone is cancelled. The plan is never opened, so this also works for a plan the phone
    /// can't open ("Start again" on the start-up screen). The problem log on this phone goes too.
    /// </summary>
    public async Task DeleteEverythingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ClearStoredDataAsync(cancellationToken).ConfigureAwait(false);
            _errors.Forget();

            // With no plan left, every reminder on the phone is cancelled.
            Rearm();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Removes every result file, the plan files, then the data key: the key goes last, once nothing it opens is left.
    /// Call under the gate, and only when she asked to delete everything or has no plan she can open.
    /// </summary>
    private async Task ClearStoredDataAsync(CancellationToken cancellationToken)
    {
        // Files first: if this fails, the plan still lists them and she can try again.
        await _attachments.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
        await _plans.DeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dataKey.ForgetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Register as a singleton: the write gate only protects callers sharing one instance.</summary>
    public void Dispose() => _gate.Dispose();

    // ---- Plumbing ------------------------------------------------------------------------

    /// <summary>A plan that has no home time zone yet (saved by an older Maia, or restored from its backup) takes the phone's.</summary>
    private void KeepHomeTimeZone(PregnancyPlan plan)
    {
        if (plan.TimeZoneId is null)
        {
            plan.SetHomeTimeZoneIfUnknown(HomeTimeZone.IdOf(_clock.TimeZone));
        }
    }

    private Task MutateAsync(Action<PregnancyPlan> change, CancellationToken cancellationToken, bool remindersFollow = true) =>
        MutateAsync(
            plan =>
            {
                change(plan);
                return Task.CompletedTask;
            },
            cancellationToken,
            remindersFollow);

    /// <summary>
    /// Loads her plan once, makes the change (which may write to disk first), saves it and reacts to its events. A
    /// change that leaves the plan as it was (the same booking again, a tick on a task already ticked) saves nothing and
    /// sets nothing again: every change to the plan moves its revision.
    /// </summary>
    /// <param name="change">The change to her plan.</param>
    /// <param name="cancellationToken">Cancels before the save.</param>
    /// <param name="remindersFollow">
    /// False for a change reminders on the phone never read (her results, her questions, her care team): they are not set
    /// again for it, unless the plan took its home time zone just now.
    /// </param>
    private async Task MutateAsync(Func<PregnancyPlan, Task> change, CancellationToken cancellationToken, bool remindersFollow = true)
    {
        PregnancyPlan plan;
        IReadOnlyList<IDomainEvent> events;

        // One writer at a time: the UI can fire commands in quick succession.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false) ?? throw new PlanNotFoundException();
            var revision = plan.Revision;
            var zone = plan.TimeZoneId;
            await change(plan).ConfigureAwait(false);
            KeepHomeTimeZone(plan);
            if (plan.Revision == revision)
            {
                return;
            }

            remindersFollow |= plan.TimeZoneId != zone;
            await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
            events = plan.DequeueEvents();
        }
        finally
        {
            _gate.Release();
        }

        await DispatchAsync(events, remindersFollow).ConfigureAwait(false);
    }

    private async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, bool remindersFollow = true)
    {
        // Reminders on the phone are set again from the saved plan, in the background: the command does not wait for
        // them, and a failure there never fails the command.
        if (remindersFollow)
        {
            Rearm();
        }

        foreach (var domainEvent in events)
        {
            if (domainEvent is ResultRemoved { BlobId: { } blobId })
            {
                await TryAsync(() => _attachments.DeleteAsync(blobId, CancellationToken.None), "attachment.delete").ConfigureAwait(false);
            }
        }
    }

    /// <summary>Sets the reminders on this phone again from the saved plan, in the background (it never throws).</summary>
    private void Rearm() => _ = _reminders?.RearmAsync();

    private async Task TryAsync(Func<Task> action, string operation)
    {
#pragma warning disable CA1031 // Reactions must not fail the user's action; failures are reported instead.
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, operation);
        }
#pragma warning restore CA1031
    }
}
