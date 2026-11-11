using System.Security.Cryptography;
using System.Text;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Application.Tests;

public sealed class PlanServiceTests : IDisposable
{
    private readonly FakeClock _clock = new(TestData.Today);
    private readonly InMemoryPlanRepository _plans = new();
    private readonly InMemoryAttachmentStore _attachments = new();
    private readonly InMemoryBackupFormat _backups = new();
    private readonly RecordingErrorReporter _errors = new();
    private readonly RecordingDataKey _dataKey = new();
    private readonly PlanService _service;

    public PlanServiceTests()
    {
        _service = new PlanService(
            _plans,
            new StubTemplateCatalog(),
            _attachments,
            _dataKey,
            _backups,
            _clock,
            _errors);
    }

    public void Dispose() => _service.Dispose();

    private static readonly Appointment GlucoseSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));

    private static FileToAttach Pdf(int size = 1024) =>
        new("report.pdf", "application/pdf", Encoding.ASCII.GetBytes(new string('x', size)));

    private Task<PregnancyPlan> CreateAsync() =>
        _service.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "gr", LocalizedText.Greek));

    [Fact]
    public async Task Creates_the_plan_once()
    {
        var plan = await CreateAsync();

        var stored = _plans.Stored!;
        Assert.Equal(plan.Id, stored.Id);
        Assert.Equal(LocalizedText.Greek, stored.Language);
        await Assert.ThrowsAsync<PlanAlreadyExistsException>(CreateAsync);
    }

    [Fact]
    public async Task Commands_without_a_plan_fail_clearly() =>
        await Assert.ThrowsAsync<PlanNotFoundException>(() => _service.DeleteEntryAsync(TestData.Glucose));

    [Fact]
    public async Task A_failed_save_changes_nothing_and_its_booking_is_never_dispatched()
    {
        await CreateAsync();
        _plans.FailNextSave = new IOException("Disk full");

        await Assert.ThrowsAsync<IOException>(() => _service.BookCheckAsync(TestData.Glucose, GlucoseSlot));

        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal(CheckState.NotBooked, glucose.State);
        Assert.Null(glucose.Appointment);

        // The next change starts from what is stored, so nothing of the failed booking is dispatched with it.
        await _service.AddQuestionAsync("Which entrance do I use?");

        Assert.Equal(CheckState.NotBooked, _plans.Stored!.GetCheck(TestData.Glucose).State);
    }

    [Fact]
    public async Task An_edit_that_fails_half_way_changes_nothing()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        // Renamed, then booked with a doctor who isn't in her care team: the booking fails, so the new name goes too.
        var slot = new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(9, 0), with: CareContactId.New());
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.EditEntryAsync(TestData.Glucose, "Sugar test", EntryWhen.Booked(slot)));
        await _service.AddQuestionAsync("Which entrance do I use?");

        Assert.Equal("appointment.contact_unknown", ex.Code);
        Assert.Equal(saves + 1, _plans.SaveCount);
        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal((CheckState.NotBooked, "Καμπύλη σακχάρου"), (glucose.State, glucose.Title.In(LocalizedText.Greek)));
    }

    [Fact]
    public async Task Her_blood_pressure_is_logged_and_deleted_and_each_change_is_saved()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        var kept = await _service.LogBloodPressureAsync(118, 76, new DateOnly(2026, 9, 30), new TimeOnly(8, 15));
        var gone = await _service.LogBloodPressureAsync(150, 95, new DateOnly(2026, 9, 30), new TimeOnly(9, 0));
        await _service.RemoveBloodPressureAsync(gone);

        Assert.Equal(saves + 3, _plans.SaveCount);
        var reading = Assert.Single(_plans.Stored!.BloodPressure);
        Assert.Equal((kept, 118, 76, new TimeOnly(8, 15)), (reading.Id, reading.Systolic, reading.Diastolic, reading.Time));
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.RemoveBloodPressureAsync(gone));
        Assert.Equal("reading.not_found", ex.Code);
    }

    [Fact]
    public async Task Deleting_everything_also_clears_the_problem_log()
    {
        await CreateAsync();

        await _service.DeleteEverythingAsync();

        Assert.Equal(1, _errors.Forgotten);
    }

    [Fact]
    public async Task Deleting_everything_removes_the_plan_and_its_files()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await _service.AttachFileAsync(Pdf());

        await _service.DeleteEverythingAsync();

        Assert.Null(_plans.Stored);
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task A_new_plan_is_at_home_in_the_phones_time_zone()
    {
        await CreateAsync();

        Assert.Equal("Europe/Athens", _plans.Stored!.TimeZoneId);
    }

    [Fact]
    public async Task Any_change_to_a_plan_from_before_the_home_zone_gives_it_the_phones()
    {
        await _plans.SaveAsync(TestData.Plan(), CancellationToken.None);

        await _service.AddQuestionAsync("Is swimming fine?");

        Assert.Equal("Europe/Athens", _plans.Stored!.TimeZoneId);
    }

    [Fact]
    public async Task A_restored_plan_keeps_its_home_zone_and_one_from_before_takes_the_new_phones()
    {
        await CreateAsync();
        using var withZone = new MemoryStream();
        await _service.ExportBackupAsync(withZone, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        _clock.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        withZone.Position = 0;
        await _service.RestoreBackupAsync(withZone, "sea-breeze-17");
        Assert.Equal("Europe/Athens", _plans.Stored!.TimeZoneId);

        await _service.DeleteEverythingAsync();
        await _plans.SaveAsync(TestData.Plan(), CancellationToken.None);
        using var withoutZone = new MemoryStream();
        await _service.ExportBackupAsync(withoutZone, "sea-breeze-17");
        await _service.DeleteEverythingAsync();

        withoutZone.Position = 0;
        await _service.RestoreBackupAsync(withoutZone, "sea-breeze-17");
        Assert.Equal("Europe/London", _plans.Stored!.TimeZoneId);
    }

    [Fact]
    public async Task A_backup_brings_back_the_plan_and_its_files_on_an_empty_phone()
    {
        await CreateAsync();
        var result = await _service.AttachFileAsync(Pdf());
        var backedUp = _plans.Stored!;
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();

        file.Position = 0;
        var restored = await _service.RestoreBackupAsync(file, "sea-breeze-17");

        var stored = _plans.Stored!;
        Assert.Equal(backedUp.Id, restored.Id);
        Assert.Equal(backedUp.Id, stored.Id);
        var blobId = Assert.Single(_attachments.Blobs).Key;
        Assert.Equal(blobId, stored.Results.Single().BlobId);
        Assert.NotEqual(backedUp.Results.Single().BlobId, blobId);
        Assert.Equal(1024, (await _service.ReadResultFileAsync(result)).Length);
    }

    [Fact]
    public async Task A_result_whose_file_is_gone_or_damaged_is_left_out_of_the_backup_not_the_whole_backup()
    {
        await CreateAsync();
        var gone = await _service.AttachFileAsync(Pdf());
        var damaged = await _service.AttachFileAsync(Pdf());
        await _service.AttachFileAsync(Pdf(2048));
        var stored = _plans.Stored!.Results;
        _attachments.Blobs.Remove(stored.Single(r => r.Id == gone).BlobId!);
        _attachments.Damaged.Add(stored.Single(r => r.Id == damaged).BlobId!);
        using var file = new MemoryStream();

        var leftOut = await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        file.Position = 0;
        var restored = await _service.RestoreBackupAsync(file, "sea-breeze-17");

        Assert.Equal(new[] { gone, damaged }, leftOut.Select(r => r.Id));
        Assert.Single(restored.Results);
        Assert.Equal(new[] { "backup.file_unreadable", "backup.file_unreadable" }, _errors.Reports.Select(r => r.Operation));
        Assert.All(_errors.Reports, r => Assert.DoesNotContain("report.pdf", r.Exception.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_backup_needs_a_password_that_is_long_enough_and_not_guessed_first()
    {
        await CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportBackupAsync(new MemoryStream(), "short"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportBackupAsync(new MemoryStream(), "sea-bree9"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportBackupAsync(new MemoryStream(), "17/01/2027"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportBackupAsync(new MemoryStream(), "Password2027!"));
    }

    [Fact]
    public async Task A_backup_is_not_restored_over_an_existing_plan()
    {
        await CreateAsync();
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        file.Position = 0;

        await Assert.ThrowsAsync<PlanAlreadyExistsException>(() => _service.RestoreBackupAsync(file, "sea-breeze-17"));
    }

    [Fact]
    public async Task A_wrong_password_restores_nothing()
    {
        await CreateAsync();
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        file.Position = 0;

        var error = await Assert.ThrowsAsync<BackupUnreadableException>(() => _service.RestoreBackupAsync(file, "sea-breeze-18"));

        Assert.Equal(BackupUnreadableException.WrongPasswordCode, error.Code);
        Assert.Null(_plans.Stored);
    }

    [Fact]
    public async Task Files_restored_before_a_failure_are_deleted_again()
    {
        await CreateAsync();
        await _service.AttachFileAsync(Pdf());
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        file.Position = 0;
        _plans.FailNextSave = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => _service.RestoreBackupAsync(file, "sea-breeze-17"));

        Assert.Null(_plans.Stored);
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task Deleting_everything_forgets_the_data_key_once_every_file_is_gone()
    {
        await CreateAsync();
        await _service.AttachFileAsync(Pdf());
        var forgotten = _dataKey.Forgotten;
        _dataKey.WhenForgotten = () =>
        {
            Assert.Empty(_attachments.Blobs);
            Assert.Null(_plans.Stored);
        };

        await _service.DeleteEverythingAsync();

        Assert.Equal(forgotten + 1, _dataKey.Forgotten);
    }

    [Fact]
    public async Task A_new_plan_starts_with_a_new_key_and_without_files_an_earlier_one_left_behind()
    {
        var leftover = await _attachments.SaveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        _dataKey.WhenForgotten = () => Assert.Empty(_attachments.Blobs);

        await CreateAsync();

        Assert.Equal(1, _dataKey.Forgotten);
        Assert.DoesNotContain(leftover, _attachments.Blobs.Keys);
    }

    [Fact]
    public async Task Deleting_everything_works_for_a_plan_this_phone_cannot_open()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await _service.AttachFileAsync(Pdf());
        _plans.Unreadable = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));

        await _service.DeleteEverythingAsync();

        Assert.Null(_plans.Unreadable);
        Assert.Null(_plans.Stored);
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task A_backup_takes_the_place_of_a_plan_this_phone_cannot_open()
    {
        await CreateAsync();
        await _service.AttachFileAsync(Pdf());
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.AddTaskAsync("Not in the backup", week: 30);
        var unreadableFile = Assert.Single(_attachments.Blobs).Key;
        _plans.Unreadable = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));
        file.Position = 0;

        await _service.RestoreBackupAsync(file, "sea-breeze-17");

        var stored = _plans.Stored!;
        Assert.DoesNotContain(stored.Tasks, t => t.Title == "Not in the backup");
        var restoredFile = Assert.Single(_attachments.Blobs).Key;
        Assert.NotEqual(unreadableFile, restoredFile);
        Assert.Equal(restoredFile, stored.Results.Single().BlobId);
    }

    [Fact]
    public async Task A_wrong_password_leaves_a_plan_this_phone_cannot_open_as_it_is()
    {
        await CreateAsync();
        await _service.AttachFileAsync(Pdf());
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        _plans.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));
        var forgotten = _dataKey.Forgotten;
        file.Position = 0;

        await Assert.ThrowsAsync<BackupUnreadableException>(() => _service.RestoreBackupAsync(file, "sea-breeze-18"));

        Assert.NotNull(_plans.Unreadable);
        Assert.NotNull(_plans.Stored);
        Assert.Single(_attachments.Blobs);
        Assert.Equal(forgotten, _dataKey.Forgotten);
    }

    [Fact]
    public async Task A_plan_from_a_newer_app_is_never_replaced_by_a_backup()
    {
        await CreateAsync();
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        _plans.Unreadable = PlanUnreadableException.FromNewerApp();
        var saves = _plans.SaveCount;
        file.Position = 0;

        var error = await Assert.ThrowsAsync<PlanUnreadableException>(() => _service.RestoreBackupAsync(file, "sea-breeze-17"));

        Assert.True(error.IsFromNewerApp);
        Assert.Equal(saves, _plans.SaveCount);
        Assert.NotNull(_plans.Unreadable);
        await Assert.ThrowsAsync<PlanUnreadableException>(CreateAsync);
    }

    [Fact]
    public async Task Attaching_a_file_stores_it_and_keeps_it_in_her_results()
    {
        await CreateAsync();

        var id = await _service.AttachFileAsync(Pdf());

        var result = Assert.Single(_plans.Stored!.Results);
        Assert.Equal(id, result.Id);
        Assert.Equal(AttachmentKind.Document, result.Kind);
        Assert.Contains(result.BlobId!, _attachments.Blobs.Keys);
        Assert.Equal(1024, (await _service.ReadResultFileAsync(id)).Length);
    }

    [Fact]
    public async Task Files_are_kept_in_her_results_apart_from_her_journey()
    {
        await CreateAsync();

        var id = await _service.AttachFileAsync(Pdf());
        var other = await _service.AttachFileAsync(Pdf(2048));

        var stored = _plans.Stored!;
        Assert.Equal(new[] { id, other }, stored.Results.Select(r => r.Id));
        Assert.Contains(stored.Results[0].BlobId!, _attachments.Blobs.Keys);
        Assert.Equal(1024, (await _service.ReadResultFileAsync(id)).Length);
    }

    [Fact]
    public async Task Removing_results_deletes_their_files()
    {
        await CreateAsync();
        var first = await _service.AttachFileAsync(Pdf());
        var second = await _service.AttachFileAsync(Pdf(2048));

        await _service.RemoveResultAsync(first);
        await _service.RemoveResultAsync(second);

        Assert.Empty(_plans.Stored!.Results);
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task A_result_keeps_its_file_through_the_tidy_and_into_a_backup()
    {
        await CreateAsync();
        var id = await _service.AttachFileAsync(Pdf());
        _clock.SetToday(TestData.Today.AddDays(2));

        Assert.Equal(0, await _service.RemoveOrphanedFilesAsync());

        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        file.Position = 0;
        await _service.RestoreBackupAsync(file, "sea-breeze-17");

        Assert.Equal(id, Assert.Single(_plans.Stored!.Results).Id);
        Assert.Equal(1024, (await _service.ReadResultFileAsync(id)).Length);
    }

    [Fact]
    public async Task Attaching_a_file_reads_the_plan_once()
    {
        await CreateAsync();
        var before = _plans.LoadCount;

        await _service.AttachFileAsync(Pdf());

        Assert.Equal(before + 1, _plans.LoadCount);
        Assert.Single(_plans.Stored!.Results);
    }

    [Theory]
    [InlineData("application/zip", 10, "attachment.type.unsupported")]
    [InlineData("application/pdf", 0, "attachment.size.empty")]
    public async Task Rejects_unsupported_or_empty_files(string mediaType, int size, string code)
    {
        await CreateAsync();

        var ex = await Assert.ThrowsAsync<AttachmentRejectedException>(
            () => _service.AttachFileAsync(new FileToAttach("x", mediaType, new byte[size])));

        Assert.Equal(code, ex.Code);
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task No_file_is_left_behind_when_saving_the_plan_fails()
    {
        await CreateAsync();
        _plans.FailNextSave = new IOException("Disk full");

        await Assert.ThrowsAsync<IOException>(() => _service.AttachFileAsync(Pdf()));

        Assert.Empty(_attachments.Blobs);
        Assert.Empty(_plans.Stored!.Results);
    }

    [Fact]
    public async Task Files_the_plan_does_not_point_at_are_tidied_once_they_are_an_hour_old()
    {
        await CreateAsync();
        _attachments.Now = () => _clock.UtcNow.AddHours(-2);
        var kept = await _service.AttachFileAsync(Pdf());
        var old = await _attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        _attachments.Now = () => _clock.UtcNow.AddMinutes(-5);
        var fresh = await _attachments.SaveAsync(new byte[] { 2 }, CancellationToken.None);

        Assert.Equal(1, await _service.RemoveOrphanedFilesAsync());

        Assert.DoesNotContain(old, _attachments.Blobs.Keys);
        Assert.Contains(fresh, _attachments.Blobs.Keys);
        Assert.Equal(1024, (await _service.ReadResultFileAsync(kept)).Length);
    }

    [Fact]
    public async Task Without_a_plan_every_old_file_is_tidied()
    {
        _attachments.Now = () => _clock.UtcNow.AddDays(-1);
        await _attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal(1, await _service.RemoveOrphanedFilesAsync());
        Assert.Empty(_attachments.Blobs);
    }

    [Fact]
    public async Task Nothing_is_tidied_when_the_plan_came_from_its_previous_copy()
    {
        await CreateAsync();
        _attachments.Now = () => _clock.UtcNow.AddDays(-1);
        await _attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        _plans.LastLoadUsedPreviousCopy = true;

        Assert.Equal(0, await _service.RemoveOrphanedFilesAsync());
        Assert.Single(_attachments.Blobs);
    }

    [Fact]
    public async Task Nothing_is_tidied_when_the_plan_cannot_be_read()
    {
        await CreateAsync();
        _attachments.Now = () => _clock.UtcNow.AddDays(-1);
        await _service.AttachFileAsync(Pdf());
        _plans.FailLoads = new CryptographicException("The key is gone.");

        await Assert.ThrowsAsync<CryptographicException>(() => _service.RemoveOrphanedFilesAsync());
        Assert.Single(_attachments.Blobs);
    }

    [Fact]
    public async Task Removing_a_result_deletes_its_file()
    {
        await CreateAsync();
        var id = await _service.AttachFileAsync(Pdf());

        await _service.RemoveResultAsync(id);

        Assert.Empty(_attachments.Blobs);
        Assert.Empty(_plans.Stored!.Results);
    }

    // ---- Entries she adds (change request #5) ---------------------------------------------

    [Fact]
    public async Task A_visit_named_after_her_doctor_joins_her_journey_in_her_language()
    {
        await CreateAsync();
        await _service.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", null, null)]);
        var doctor = _plans.Stored!.CareTeam.Contacts.Single();
        var slot = new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), with: doctor.Id);

        await _service.AddEntryAsync(EntryTitles.DoctorVisit(doctor), EntryKind.DoctorVisit, EntryWhen.Booked(slot));

        var visit = Assert.Single(_plans.Stored!.DoctorVisits());
        Assert.Equal((CheckState.Booked, slot), (visit.State, visit.Appointment));
        Assert.Equal("Επίσκεψη: Dr. Eleni Papadaki", visit.Title.In(LocalizedText.Greek));
    }

    [Fact]
    public async Task A_new_plan_holds_only_the_checks_she_kept_whose_weeks_are_still_ahead()
    {
        await CreateAsync();

        // The booking visit's weeks (8–10) are over; NIPT and the growth scan are off unless she switches them on.
        Assert.Equal(
            new[] { TestData.Anomaly, TestData.Glucose, TestData.Checkup25, TestData.DueDateMilestone },
            _plans.Stored!.Checks.Select(c => c.Id));
    }

    [Fact]
    public async Task Booking_a_check_saves_it_booked_and_the_same_booking_again_saves_nothing()
    {
        await CreateAsync();

        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var saves = _plans.SaveCount;
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);

        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal((CheckState.Booked, GlucoseSlot), (glucose.State, glucose.Appointment));
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task Editing_an_entry_without_changing_its_name_keeps_it_in_every_language()
    {
        await CreateAsync();

        await _service.EditEntryAsync(TestData.Glucose, " Καμπύλη σακχάρου ", EntryWhen.NotBookedInWeek(26));

        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal(TestData.Text("Glucose test", "Καμπύλη σακχάρου"), glucose.Title);
        Assert.Equal((CheckState.NotBooked, 26), (glucose.State, glucose.Week));
    }

    [Fact]
    public async Task Editing_an_entry_with_a_new_name_takes_her_words_and_its_new_timing_in_one_save()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        await _service.EditEntryAsync(TestData.Glucose, "  Sugar curve  ", EntryWhen.Booked(GlucoseSlot));

        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal(saves + 1, _plans.SaveCount);
        Assert.Equal(LocalizedText.Plain("Sugar curve"), glucose.Title);
        Assert.Equal((CheckState.Booked, GlucoseSlot), (glucose.State, glucose.Appointment));
    }

    [Fact]
    public async Task Saving_an_edit_with_nothing_changed_saves_nothing()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var saves = _plans.SaveCount;

        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", EntryWhen.Booked(GlucoseSlot));

        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task Editing_an_entry_keeps_its_note_in_every_language_until_she_changes_it()
    {
        await CreateAsync();
        var when = EntryWhen.NotBookedInWeek(24);
        var typical = TestData.Text("Glucose test details", "Καμπύλη σακχάρου");

        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", when, " Καμπύλη σακχάρου ");
        Assert.Equal(typical, _plans.Stored!.GetCheck(TestData.Glucose).Description);

        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", when, " Bring the referral ");
        Assert.Equal(LocalizedText.Plain("Bring the referral"), _plans.Stored!.GetCheck(TestData.Glucose).Description);

        // No note given leaves hers as it is.
        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", when);
        Assert.Equal(LocalizedText.Plain("Bring the referral"), _plans.Stored!.GetCheck(TestData.Glucose).Description);

        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", when, "  ");
        Assert.Null(_plans.Stored!.GetCheck(TestData.Glucose).Description);

        var saves = _plans.SaveCount;
        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", when, string.Empty);
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task An_entry_she_adds_keeps_the_note_she_wrote_and_none_when_she_wrote_nothing()
    {
        await CreateAsync();

        var dentist = await _service.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Third), "  Ask about cleaning  ");
        var scan = await _service.AddEntryAsync(TestData.Text("Extra ultrasound", "Επιπλέον υπερηχογράφημα"), EntryKind.Exam, EntryWhen.NotBookedInWeek(30), "Fasting");
        var visit = await _service.AddEntryAsync("Visit", EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(30), "   ");
        var tour = await _service.AddEntryAsync("Hospital tour", EntryKind.HospitalVisit, EntryWhen.NotBookedIn(Trimester.AfterBirth));

        var plan = _plans.Stored!;
        Assert.Equal(LocalizedText.Plain("Ask about cleaning"), plan.GetCheck(dentist).Description);
        Assert.Equal(LocalizedText.Plain("Fasting"), plan.GetCheck(scan).Description);
        Assert.Null(plan.GetCheck(visit).Description);
        Assert.Null(plan.GetCheck(tour).Description);
        Assert.Equal(Trimester.AfterBirth, plan.GetCheck(tour).TrimesterOnly);
    }

    [Fact]
    public async Task A_task_is_added_and_changed_for_a_trimester_or_a_week()
    {
        await CreateAsync();

        var bag = await _service.AddTaskAsync("Pack the hospital bag", trimester: Trimester.Third);
        await _service.EditTaskAsync(bag, "Pack the hospital bag", week: 36);

        var task = _plans.Stored!.GetTask(bag);
        Assert.Equal(((Trimester?)null, (int?)36, (DateOnly?)null), (task.Trimester, task.Week, task.DueDate));
        await Assert.ThrowsAsync<DomainException>(() => _service.AddTaskAsync("Buy a pillow"));
    }

    [Fact]
    public async Task Editing_an_entry_can_say_it_was_already_done_in_a_trimester()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);

        await _service.EditEntryAsync(TestData.Glucose, "Καμπύλη σακχάρου", EntryWhen.DoneIn(Trimester.Second));

        var glucose = _plans.Stored!.GetCheck(TestData.Glucose);
        Assert.Equal((CheckState.Done, (Trimester?)Trimester.Second), (glucose.State, glucose.TrimesterOnly));
        Assert.Null(glucose.Appointment);
    }

    [Fact]
    public async Task Deleting_an_entry_takes_it_off_her_journey_for_good()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var added = await _service.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Third));

        await _service.DeleteEntryAsync(TestData.Glucose);
        await _service.DeleteEntryAsync(added);

        var plan = _plans.Stored!;
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Glucose || c.Id == added);
        Assert.Equal(TestData.DueDate, plan.Dating.DueDate);
        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteEntryAsync(TestData.Glucose));
    }

    [Fact]
    public async Task The_due_date_cannot_be_deleted()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.DeleteEntryAsync(TestData.DueDateMilestone));

        Assert.Equal("check.is_milestone", ex.Code);
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Theory]
    [InlineData(EntryKind.DoctorVisit)]
    [InlineData(EntryKind.Exam)]
    [InlineData(EntryKind.HospitalVisit)]
    [InlineData(EntryKind.Other)]
    public async Task An_entry_she_books_keeps_the_name_she_gave_it(EntryKind kind)
    {
        await CreateAsync();
        var slot = new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), "Clinic");

        var id = await _service.AddEntryAsync("  Hospital tour  ", kind, EntryWhen.Booked(slot));

        var entry = _plans.Stored!.GetCheck(id);
        Assert.Equal((kind, CheckState.Booked, "Hospital tour"), (entry.Kind!.Value, entry.State, entry.Title.In(LocalizedText.Greek)));
        Assert.Equal(slot, entry.Appointment);
    }

    [Fact]
    public async Task An_entry_already_done_or_not_booked_yet_stands_in_the_week_or_trimester_she_gave()
    {
        await CreateAsync();

        var done = await _service.AddEntryAsync("Blood test", EntryKind.Exam, EntryWhen.DoneInWeek(22));
        var doneInTrimester = await _service.AddEntryAsync("Eye test", EntryKind.Exam, EntryWhen.DoneIn(Trimester.First));
        var waiting = await _service.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Third));
        var waitingInWeek = await _service.AddEntryAsync("Hospital tour", EntryKind.HospitalVisit, EntryWhen.NotBookedInWeek(34));

        var stored = _plans.Stored!;
        Assert.Equal((CheckState.Done, 22, (Trimester?)null), (stored.GetCheck(done).State, stored.GetCheck(done).Week, stored.GetCheck(done).TrimesterOnly));
        Assert.Equal((CheckState.Done, Trimester.First), (stored.GetCheck(doneInTrimester).State, stored.GetCheck(doneInTrimester).Trimester));
        Assert.Equal((CheckState.NotBooked, Trimester.Third), (stored.GetCheck(waiting).State, stored.GetCheck(waiting).Trimester));
        Assert.Equal((CheckState.NotBooked, 34), (stored.GetCheck(waitingInWeek).State, stored.GetCheck(waitingInWeek).Week));
        Assert.All(new[] { done, doneInTrimester, waiting, waitingInWeek }, id => Assert.Null(stored.GetCheck(id).Appointment));
    }

    [Fact]
    public async Task An_entry_booked_for_a_time_already_passed_reads_done()
    {
        await CreateAsync();
        var earlier = new Appointment(TestData.Today, new TimeOnly(9, 0));

        var id = await _service.AddEntryAsync("Blood test", EntryKind.Exam, EntryWhen.Booked(earlier));

        var plan = _plans.Stored!;
        Assert.Equal(CheckState.Booked, plan.GetCheck(id).State);
        Assert.Equal(CheckStatus.Done, plan.StatusOf(plan.GetCheck(id), TestData.Today, new TimeOnly(12, 0)));
    }

    [Fact]
    public async Task A_name_too_long_for_an_entry_is_refused_and_nothing_is_saved()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.AddEntryAsync(new string('a', PlannedCheck.MaxTitleLength + 1), EntryKind.Other, EntryWhen.NotBookedIn(Trimester.Third)));

        Assert.Equal("entry.title.too_long", ex.Code);
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task Linking_contacts_counts_only_new_ones()
    {
        await CreateAsync();
        var picked = new[]
        {
            new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "device-1"),
            new ContactToLink(CareRole.ObGyn, "Papadaki Clinic", "+30 210 000 2210", "Reception", "device-2"),
        };

        Assert.Equal(2, await _service.LinkContactsAsync(picked));
        Assert.Equal(0, await _service.LinkContactsAsync(picked.Take(1)));
        Assert.Equal(2, _plans.Stored!.CareTeam.For(CareRole.ObGyn).Count);
    }

    private sealed class StubTemplateCatalog : IPlanTemplateCatalog
    {
        public IReadOnlyList<string> SupportedCountries => ["GR"];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(TestData.Template());
    }
}
