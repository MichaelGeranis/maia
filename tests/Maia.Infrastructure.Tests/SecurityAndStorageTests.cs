using System.Security.Cryptography;
using System.Text;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Infrastructure.Attachments;
using Maia.Infrastructure.Persistence;
using Maia.Infrastructure.Security;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

/// <summary>Gives each test its own temporary folder and cleans it up.</summary>
public abstract class TempFolderTest : IDisposable
{
    protected TempFolderTest()
    {
        Folder = Path.Combine(Path.GetTempPath(), "maia-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Cipher = new AesGcmCipher(new InMemoryKeyProvider(AesGcmCipher.GenerateKey()));
    }

    protected string Folder { get; }

    protected AesGcmCipher Cipher { get; }

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>A data key in memory that counts how often it is asked for: once per encryption or decryption.</summary>
internal sealed class CountingKeyProvider : IKeyProvider
{
    private readonly InMemoryKeyProvider _inner = new(AesGcmCipher.GenerateKey());

    public int Uses { get; set; }

    public ValueTask<byte[]> GetKeyAsync(CancellationToken cancellationToken)
    {
        Uses++;
        return _inner.GetKeyAsync(cancellationToken);
    }
}

public sealed class AesGcmCipherTests
{
    private readonly AesGcmCipher _cipher = new(new InMemoryKeyProvider(AesGcmCipher.GenerateKey()));

    [Fact]
    public async Task Round_trips_and_hides_the_plaintext()
    {
        var secret = Encoding.UTF8.GetBytes("Καμπύλη σακχάρου: 92 mg/dL");

        var envelope = await _cipher.EncryptAsync(secret, "plan", CancellationToken.None);

        Assert.Equal(AesGcmCipher.FormatVersion, AesGcmCipher.PeekVersion(envelope));
        Assert.DoesNotContain("mg/dL", Encoding.UTF8.GetString(envelope), StringComparison.Ordinal);
        Assert.Equal(secret, await _cipher.DecryptAsync(envelope, "plan", CancellationToken.None));
    }

    [Fact]
    public async Task Same_input_encrypts_differently_each_time()
    {
        var a = await _cipher.EncryptAsync(new byte[16], "plan", CancellationToken.None);
        var b = await _cipher.EncryptAsync(new byte[16], "plan", CancellationToken.None);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Detects_tampering_and_swapped_purpose()
    {
        var envelope = await _cipher.EncryptAsync("hello"u8.ToArray(), "blob:a", CancellationToken.None);

        await Assert.ThrowsAnyAsync<CryptographicException>(() => _cipher.DecryptAsync(envelope, "blob:b", CancellationToken.None));

        envelope[^1] ^= 0xFF;
        await Assert.ThrowsAnyAsync<CryptographicException>(() => _cipher.DecryptAsync(envelope, "blob:a", CancellationToken.None));
    }

    [Fact]
    public async Task Fails_with_another_key()
    {
        var envelope = await _cipher.EncryptAsync("hello"u8.ToArray(), "plan", CancellationToken.None);
        var other = new AesGcmCipher(new InMemoryKeyProvider(AesGcmCipher.GenerateKey()));

        await Assert.ThrowsAnyAsync<CryptographicException>(() => other.DecryptAsync(envelope, "plan", CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_keys_of_the_wrong_size()
    {
        var weak = new AesGcmCipher(new InMemoryKeyProvider(new byte[16]));

        await Assert.ThrowsAnyAsync<CryptographicException>(() => weak.EncryptAsync(new byte[1], "plan", CancellationToken.None));
    }
}

public sealed class EncryptedPlanRepositoryTests : TempFolderTest
{
    private EncryptedPlanRepository Repository => new(Folder, Cipher);

    private static PregnancyPlan RichPlan()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "device-1");
        plan.LinkContact(doctor);
        plan.Book(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30), "Papadaki Clinic", doctor.Id));
        plan.Reschedule(TestData.Anomaly, EntryWhen.DoneInWeek(20));
        plan.AttachResult(ResultAttachment.ForFile(AttachmentId.New(), "20-week scan.jpg", "image/jpeg", 2048, "0123456789abcdef0123456789abcdef", DateTimeOffset.UnixEpoch));
        plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedIn(Trimester.Third));
        plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        plan.SetQuestionAnswered(plan.AddQuestion("Is swimming fine?"), answered: true);
        plan.AddQuestion("When do we talk about the birth plan?");
        plan.AddEntry(TestData.Text("Visit with Dr. Eleni Papadaki", "Επίσκεψη: Dr. Eleni Papadaki"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), "Clinic", doctor.Id)));
        plan.AddEntry(LocalizedText.Plain("Hospital tour"), EntryKind.HospitalVisit, EntryWhen.DoneIn(Trimester.Second));
        plan.AttachResult(ResultAttachment.ForFile(AttachmentId.New(), "bloods.pdf", "application/pdf", 4096, "fedcba9876543210fedcba9876543210", DateTimeOffset.UnixEpoch));
        plan.ChangeLanguage(LocalizedText.Greek);
        plan.SetHomeTimeZoneIfUnknown("Europe/Athens");
        return plan;
    }

    [Fact]
    public async Task Returns_null_when_nothing_is_stored() =>
        Assert.Null(await Repository.LoadAsync(CancellationToken.None));

    [Fact]
    public async Task Round_trips_every_part_of_the_plan()
    {
        var plan = RichPlan();

        await Repository.SaveAsync(plan, CancellationToken.None);
        var loaded = await Repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(plan.Id, loaded.Id);
        Assert.Equal(plan.Revision, loaded.Revision);
        Assert.Equal(plan.Dating, loaded.Dating);
        Assert.Equal(plan.Template, loaded.Template);
        Assert.Equal("el", loaded.Language);
        Assert.Equal(plan.Checks.Select(c => (c.Id, c.State, c.Window, c.Appointment, c.TrimesterOnly, c.Kind)), loaded.Checks.Select(c => (c.Id, c.State, c.Window, c.Appointment, c.TrimesterOnly, c.Kind)));
        Assert.Equal(plan.Checks.Select(c => c.Title), loaded.Checks.Select(c => c.Title));
        Assert.Equal(plan.CareTeam.Contacts, loaded.CareTeam.Contacts);
        Assert.Equal(plan.Tasks.Select(t => (t.Id, t.Title, t.DueDate, t.DueTime, t.IsDone)), loaded.Tasks.Select(t => (t.Id, t.Title, t.DueDate, t.DueTime, t.IsDone)));
        Assert.Equal(plan.Questions.Select(q => (q.Id, q.Text, q.IsAnswered)), loaded.Questions.Select(q => (q.Id, q.Text, q.IsAnswered)));
        Assert.Equal(plan.Results, loaded.Results);
        Assert.Equal(3, loaded.Checks.Count(c => c.Kind is not null));
        Assert.Equal(2, loaded.Checks.Count(c => c.TrimesterOnly is not null));
        Assert.Equal("Europe/Athens", loaded.TimeZoneId);
    }

    [Fact]
    public async Task An_entry_given_only_its_trimester_keeps_it_not_booked_or_done()
    {
        var plan = TestData.Plan();
        var later = plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedIn(Trimester.Third));
        var earlier = plan.AddEntry(LocalizedText.Plain("Hospital tour"), EntryKind.HospitalVisit, EntryWhen.DoneIn(Trimester.First));
        var inAWeek = plan.AddEntry(LocalizedText.Plain("Dentist"), EntryKind.Other, EntryWhen.NotBookedInWeek(30));

        await Repository.SaveAsync(plan, CancellationToken.None);
        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Equal((CheckState.NotBooked, (Trimester?)Trimester.Third), (loaded.GetCheck(later).State, loaded.GetCheck(later).TrimesterOnly));
        Assert.Equal((CheckState.Done, (Trimester?)Trimester.First), (loaded.GetCheck(earlier).State, loaded.GetCheck(earlier).TrimesterOnly));
        Assert.Equal((CheckState.NotBooked, (Trimester?)null), (loaded.GetCheck(inAWeek).State, loaded.GetCheck(inAWeek).TrimesterOnly));
        Assert.Equal(plan.GetCheck(later).Window, loaded.GetCheck(later).Window);
        Assert.Equal(plan.GetCheck(earlier).Window, loaded.GetCheck(earlier).Window);
        Assert.Equal(Trimester.Third, loaded.GetCheck(later).Trimester);
        Assert.Equal(Trimester.First, loaded.GetCheck(earlier).Trimester);
    }

    [Fact]
    public async Task Tasks_for_a_trimester_or_a_week_an_entry_after_birth_and_her_notes_come_back_as_saved()
    {
        // One saved before tasks needed a trimester or a week still loads, with neither.
        var plan = TestData.Plan().WithTasks([(null, null, false)]);
        var bag = plan.AddTask("Pack the hospital bag", trimester: Trimester.AfterBirth);
        var classes = plan.AddTask("Book the antenatal class", week: 28);
        var visit = plan.AddEntry(LocalizedText.Plain("Six-week check"), EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.AfterBirth), LocalizedText.Plain("Bring the baby's booklet"));

        await Repository.SaveAsync(plan, CancellationToken.None);
        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Equal(plan.Tasks.Select(t => (t.Id, t.Trimester, t.Week)), loaded.Tasks.Select(t => (t.Id, t.Trimester, t.Week)));
        Assert.Equal(Trimester.AfterBirth, loaded.GetTask(bag).Trimester);
        Assert.Equal(28, loaded.GetTask(classes).Week);
        Assert.Equal((Trimester?)Trimester.AfterBirth, loaded.GetCheck(visit).TrimesterOnly);
        Assert.Equal(LocalizedText.Plain("Bring the baby's booklet"), loaded.GetCheck(visit).Description);
    }

    [Fact]
    public async Task Her_blood_pressure_readings_come_back_as_logged()
    {
        var plan = TestData.Plan();
        var morning = plan.LogBloodPressure(118, 76, new DateOnly(2026, 10, 10), new TimeOnly(8, 30));
        var evening = plan.LogBloodPressure(124, 80, new DateOnly(2026, 10, 10), new TimeOnly(20, 5));

        await Repository.SaveAsync(plan, CancellationToken.None);
        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Equal(
            [(morning, 118, 76, new DateOnly(2026, 10, 10), new TimeOnly(8, 30)), (evening, 124, 80, new DateOnly(2026, 10, 10), new TimeOnly(20, 5))],
            loaded.BloodPressure.Select(r => (r.Id, r.Systolic, r.Diastolic, r.Date, r.Time)));
    }

    [Fact]
    public async Task A_check_she_renamed_or_moved_is_still_hers_after_a_reload()
    {
        var plan = TestData.Plan();
        plan.Rename(TestData.Glucose, LocalizedText.Plain("My sugar test"));
        plan.Reschedule(TestData.Anomaly, EntryWhen.NotBookedInWeek(22));

        await Repository.SaveAsync(plan, CancellationToken.None);
        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.True(loaded.GetCheck(TestData.Glucose).IsEdited);
        Assert.True(loaded.GetCheck(TestData.Anomaly).IsEdited);
        Assert.False(loaded.GetCheck(TestData.Checkup25).IsEdited);
    }

    [Fact]
    public async Task Loading_a_plan_that_has_not_changed_on_disk_does_not_decrypt_it_again()
    {
        var keys = new CountingKeyProvider();
        var repository = new EncryptedPlanRepository(Folder, new AesGcmCipher(keys));
        await repository.SaveAsync(RichPlan(), CancellationToken.None);
        keys.Uses = 0;

        var first = await repository.LoadAsync(CancellationToken.None);
        var second = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(0, keys.Uses);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(first.Checks.Select(c => (c.Id, c.State, c.Appointment)), second.Checks.Select(c => (c.Id, c.State, c.Appointment)));
    }

    [Fact]
    public async Task Each_load_gets_a_plan_of_its_own_to_change()
    {
        await Repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        var repository = Repository;

        var changed = await repository.LoadAsync(CancellationToken.None);
        changed!.AddTask("Not saved", week: 30);
        var again = await repository.LoadAsync(CancellationToken.None);

        Assert.Empty(again!.Tasks);
    }

    [Fact]
    public async Task A_plan_saved_by_another_copy_of_the_store_is_read_again()
    {
        var repository = Repository;
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        Assert.Empty((await repository.LoadAsync(CancellationToken.None))!.Tasks);

        var other = Repository;
        var plan = (await other.LoadAsync(CancellationToken.None))!;
        plan.AddTask("Saved elsewhere", week: 30);
        await other.SaveAsync(plan, CancellationToken.None);

        Assert.Equal("Saved elsewhere", Assert.Single((await repository.LoadAsync(CancellationToken.None))!.Tasks).Title);
    }

    [Fact]
    public async Task Upgrades_a_schema_3_plan_saved_before_the_home_time_zone_existed()
    {
        // A plan exactly as schema 3 stored it: a booking, a timed task, a question and her next visit, but no zone.
        var v3 = """
            {"schemaVersion":3,"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":9,"dueDate":"2027-01-17",
             "templateId":"test-gr","templateVersion":1,"language":"el","createdAt":"2026-09-30T09:00:00+03:00",
             "checks":[{"id":"checkup-25","origin":"Template","title":{"en":"Check-up","el":"Επίσκεψη"},
                        "category":"Routine","fromWeek":25,"toWeek":25,"state":"Booked",
                        "appointment":{"date":"2026-10-06","time":"10:30:00","location":"Clinic"},"results":[]}],
             "tasks":[{"id":"0b8f0c55-4a3e-4d8a-9a57-2d3b0c3f1e21","title":"Call clinic","dueDate":"2026-10-01","dueTime":"09:00:00","isDone":false},
                      {"id":"1c9f0c55-4a3e-4d8a-9a57-2d3b0c3f1e22","title":"Pack the bag","isDone":false}],
             "contacts":[],
             "questions":[{"id":"3d0c4f0e-1b2a-4c5d-8e9f-0a1b2c3d4e5f","text":"Is swimming fine?","isAnswered":true}],
             "nextVisit":{"date":"2026-10-20","time":"11:00:00"}}
            """u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName), await Cipher.EncryptAsync(v3, "plan", CancellationToken.None));

        var loaded = await Repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded.TimeZoneId);
        Assert.Equal(9, loaded.Revision);
        Assert.Equal("el", loaded.Language);
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30), "Clinic"), loaded.GetCheck(TestData.Checkup25).Appointment);
        Assert.Equal(new[] { ("Call clinic", true), ("Pack the bag", false) }, loaded.Tasks.Select(t => (t.Title, t.IsScheduled)));
        Assert.True(Assert.Single(loaded.Questions).IsAnswered);

        // Her next visit is a booked doctor visit; with no doctor linked it is an appointment, in her language.
        var visit = Assert.Single(loaded.DoctorVisits());
        Assert.Equal((CheckState.Booked, new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0))), (visit.State, visit.Appointment));
        Assert.Equal("Ραντεβού", visit.Title.In(loaded.Language));

        // Maia gives it the phone's zone and saves it in the current schema.
        loaded.SetHomeTimeZoneIfUnknown("Europe/Athens");
        await Repository.SaveAsync(loaded, CancellationToken.None);
        var json = System.Text.Json.Nodes.JsonNode.Parse(await Cipher.DecryptAsync(await File.ReadAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName)), "plan", CancellationToken.None))!;
        Assert.Equal(PlanDocument.CurrentSchemaVersion, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Europe/Athens", json["timeZone"]!.GetValue<string>());
        Assert.Equal("Europe/Athens", (await Repository.LoadAsync(CancellationToken.None))!.TimeZoneId);
    }

    /// <summary>
    /// A plan exactly as schema 4 stored it: results under a check, a booking, her next visit with her OB-GYN, a check
    /// her doctor asked for picked from Maia's suggestions before they were saved in both languages (in Greek, kept as
    /// English), one in English, and one she typed.
    /// </summary>
    private static readonly byte[] SchemaFour = """
        {"schemaVersion":4,"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":12,"dueDate":"2027-01-17",
         "templateId":"test-gr","templateVersion":1,"language":"el","createdAt":"2026-09-30T09:00:00+03:00",
         "checks":[{"id":"anomaly","origin":"Template","title":{"en":"Anomaly scan","el":"Υπερηχογράφημα β' επιπέδου"},
                    "category":"Routine","fromWeek":20,"toWeek":24,"state":"Done","completedOn":"2026-09-02",
                    "results":[{"id":"2b9a7c1e-0d3f-4e5a-8b6c-7d8e9f0a1b2c","kind":"Photo","displayName":"20-week scan.jpg",
                                "addedAt":"2026-09-02T10:00:00+03:00","blobId":"0123456789abcdef0123456789abcdef",
                                "mediaType":"image/jpeg","sizeBytes":2048},
                               {"id":"3c0b8d2f-1e4a-4f6b-9c7d-8e9f0a1b2c3d","kind":"Link","displayName":"Lab portal",
                                "addedAt":"2026-09-03T10:00:00+03:00","link":"https://lab.example/r/42"}]},
                   {"id":"checkup-25","origin":"Template","title":{"en":"Check-up","el":"Επίσκεψη"},"category":"Routine",
                    "fromWeek":25,"toWeek":25,"state":"Booked","appointment":{"date":"2026-10-06","time":"10:30:00","location":"Clinic"},
                    "results":[]},
                   {"id":"custom-0a1b2c3d4e5f","origin":"AddedByParent","title":{"en":"Επανέλεγχος σιδήρου"},"category":"IfAdvised",
                    "fromWeek":24,"toWeek":26,"state":"Pending","results":[]},
                   {"id":"custom-1b2c3d4e5f60","origin":"AddedByParent","title":{"en":"Thyroid test"},"category":"IfAdvised",
                    "fromWeek":24,"toWeek":26,"state":"Pending","results":[]},
                   {"id":"custom-5f4e3d2c1b0a","origin":"AddedByParent","title":{"en":"Ferritin"},"category":"IfAdvised",
                    "fromWeek":24,"toWeek":26,"state":"Pending","results":[]}],
         "tasks":[{"id":"0b8f0c55-4a3e-4d8a-9a57-2d3b0c3f1e21","title":"Call clinic","dueDate":"2026-10-01","dueTime":"09:00:00","isDone":false}],
         "contacts":[{"id":"9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a","role":"ObGyn","displayName":"Dr. Eleni Papadaki",
                      "phoneNumber":"+30 690 000 4567","label":"Mobile"}],
         "questions":[{"id":"3d0c4f0e-1b2a-4c5d-8e9f-0a1b2c3d4e5f","text":"Is swimming fine?","isAnswered":false}],
         "nextVisit":{"date":"2026-10-20","time":"11:00:00","location":"Clinic","contactId":"9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a"},
         "timeZone":"Europe/Athens"}
        """u8.ToArray();

    private async Task StoreAsync(byte[] json) =>
        await File.WriteAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName), await Cipher.EncryptAsync(json, "plan", CancellationToken.None));

    [Fact]
    public async Task Upgrades_a_schema_4_plan_her_next_visit_becoming_a_booked_doctor_visit()
    {
        await StoreAsync(SchemaFour);

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        var visit = Assert.Single(loaded.DoctorVisits());
        Assert.Equal(new CheckId(PlanDocumentMigrator.VisitEntryId), visit.Id);
        Assert.Equal((CheckOrigin.AddedByParent, CheckState.Booked), (visit.Origin, visit.State));
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), "Clinic", new CareContactId(new Guid("9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a"))), visit.Appointment);
        Assert.Equal(("Visit with Dr. Eleni Papadaki", "Επίσκεψη: Dr. Eleni Papadaki"), (visit.Title.In("en"), visit.Title.In("el")));
        Assert.Equal(Domain.Templates.WeekWindow.OfWeek(27), visit.Window);
        Assert.Same(visit, loaded.NextAppointment(new DateOnly(2026, 10, 7)));

        // Read again before any save, it is the same entry, so its reminders and the screens keep pointing at it.
        Assert.Equal(visit.Id, Assert.Single((await Repository.LoadAsync(CancellationToken.None))!.DoctorVisits()).Id);
    }

    [Fact]
    public async Task Upgrading_a_schema_4_plan_keeps_its_results_apart_and_gives_her_checks_their_kind_and_both_names()
    {
        await StoreAsync(SchemaFour);

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        // Lab links are no longer kept: the link goes, the photo stays.
        Assert.Equal("20-week scan.jpg", Assert.Single(loaded.Results).DisplayName);
        Assert.Equal("0123456789abcdef0123456789abcdef", loaded.Results[0].BlobId);
        Assert.Null(loaded.GetCheck(TestData.Anomaly).Kind);
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30), "Clinic"), loaded.GetCheck(TestData.Checkup25).Appointment);

        var iron = loaded.GetCheck(new CheckId("custom-0a1b2c3d4e5f"));
        var thyroid = loaded.GetCheck(new CheckId("custom-1b2c3d4e5f60"));
        var typed = loaded.GetCheck(new CheckId("custom-5f4e3d2c1b0a"));
        Assert.All(new[] { iron, thyroid, typed }, c => Assert.Equal(EntryKind.Exam, c.Kind));
        Assert.Equal(TestData.Text("Iron recheck", "Επανέλεγχος σιδήρου"), iron.Title);
        Assert.Equal(TestData.Text("Thyroid test", "Έλεγχος θυρεοειδούς"), thyroid.Title);
        Assert.Equal(LocalizedText.Plain("Ferritin"), typed.Title);
        Assert.Equal(("Europe/Athens", 12L), (loaded.TimeZoneId, loaded.Revision));
    }

    [Fact]
    public async Task A_schema_4_plan_saved_again_is_in_the_current_schema_without_its_old_next_visit()
    {
        await StoreAsync(SchemaFour);
        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        loaded.AddQuestion("When do we talk about the birth plan?");
        await Repository.SaveAsync(loaded, CancellationToken.None);

        var json = System.Text.Json.Nodes.JsonNode.Parse(await Cipher.DecryptAsync(await File.ReadAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName)), "plan", CancellationToken.None))!;
        Assert.Equal(PlanDocument.CurrentSchemaVersion, json["schemaVersion"]!.GetValue<int>());
        Assert.Null(json["nextVisit"]);
        Assert.Null(json["untaggedResults"]);
        Assert.Single(json["results"]!.AsArray());
        Assert.All(json["checks"]!.AsArray(), c => Assert.Null(c!["results"]));
        var again = (await Repository.LoadAsync(CancellationToken.None))!;
        Assert.Equal(loaded.Checks.Select(c => (c.Id, c.Kind, c.State, c.Appointment)), again.Checks.Select(c => (c.Id, c.Kind, c.State, c.Appointment)));
    }

    [Fact]
    public async Task Upgrading_a_schema_5_plan_keeps_every_result_in_one_list_apart_from_her_entries()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(SchemaFour)!.AsObject();
        json["schemaVersion"] = 5;
        json.Remove("nextVisit");
        json["untaggedResults"] = System.Text.Json.Nodes.JsonNode.Parse("""
            [{"id":"4d1c9e3a-2f5b-4a7c-8d9e-0f1a2b3c4d5e","kind":"Document","displayName":"bloods.pdf",
              "addedAt":"2026-09-20T10:00:00+03:00","blobId":"fedcba9876543210fedcba9876543210",
              "mediaType":"application/pdf","sizeBytes":4096}]
            """);
        await StoreAsync(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()));

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Equal(new[] { "20-week scan.jpg", "bloods.pdf" }, loaded.Results.Select(r => r.DisplayName));
        Assert.Equal(new[] { "0123456789abcdef0123456789abcdef", "fedcba9876543210fedcba9876543210" }, loaded.Results.Select(r => r.BlobId));
        Assert.Equal(CheckState.Done, loaded.GetCheck(TestData.Anomaly).State);
    }

    [Fact]
    public async Task Upgrading_a_schema_6_plan_drops_its_lab_links_and_keeps_its_files()
    {
        await StoreAsync("""
            {"schemaVersion":6,"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":3,"dueDate":"2027-01-17",
             "templateId":"test-gr","templateVersion":1,"language":"en","createdAt":"2026-09-30T09:00:00+03:00",
             "checks":[],"tasks":[],"contacts":[],"questions":[],
             "results":[{"id":"2b9a7c1e-0d3f-4e5a-8b6c-7d8e9f0a1b2c","kind":"Photo","displayName":"20-week scan.jpg",
                         "addedAt":"2026-09-02T10:00:00+03:00","blobId":"0123456789abcdef0123456789abcdef",
                         "mediaType":"image/jpeg","sizeBytes":2048},
                        {"id":"3c0b8d2f-1e4a-4f6b-9c7d-8e9f0a1b2c3d","kind":"Link","displayName":"Lab portal",
                         "addedAt":"2026-09-03T10:00:00+03:00","link":"https://lab.example/r/42"}],
             "timeZone":"Europe/Athens"}
            """u8.ToArray());

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;
        await Repository.SaveAsync(loaded, CancellationToken.None);

        var result = Assert.Single(loaded.Results);
        Assert.Equal((AttachmentKind.Photo, "20-week scan.jpg"), (result.Kind, result.DisplayName));
        var json = System.Text.Json.Nodes.JsonNode.Parse(await Cipher.DecryptAsync(await File.ReadAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName)), "plan", CancellationToken.None))!;
        Assert.Equal(PlanDocument.CurrentSchemaVersion, json["schemaVersion"]!.GetValue<int>());
        Assert.DoesNotContain("lab.example", json.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_schema_4_next_visit_at_the_day_and_time_of_a_booking_is_that_booking_not_a_second_entry()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(SchemaFour)!.AsObject();
        json["nextVisit"] = System.Text.Json.Nodes.JsonNode.Parse("""{"date":"2026-10-06","time":"10:30:00"}""");
        await StoreAsync(Encoding.UTF8.GetBytes(json.ToJsonString()));

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Empty(loaded.DoctorVisits());
        Assert.Equal(5, loaded.Checks.Count);
    }

    [Fact]
    public async Task Upgrading_a_schema_4_plan_stands_the_checks_she_booked_or_did_at_their_day()
    {
        // All three were added at 24 weeks: one then booked months later, one done, one still waiting for a date.
        var json = System.Text.Json.Nodes.JsonNode.Parse(SchemaFour)!.AsObject();
        var checks = json["checks"]!.AsArray();
        var thyroid = checks.Single(c => c!["id"]!.GetValue<string>() == "custom-1b2c3d4e5f60")!.AsObject();
        thyroid["state"] = "Done";
        thyroid["completedOn"] = "2026-09-25";
        var ferritin = checks.Single(c => c!["id"]!.GetValue<string>() == "custom-5f4e3d2c1b0a")!.AsObject();
        ferritin["state"] = "Booked";
        ferritin["appointment"] = System.Text.Json.Nodes.JsonNode.Parse("""{"date":"2027-01-05","time":"09:30:00"}""");
        await StoreAsync(Encoding.UTF8.GetBytes(json.ToJsonString()));

        var loaded = (await Repository.LoadAsync(CancellationToken.None))!;

        Assert.Equal(Domain.Templates.WeekWindow.OfWeek(38), loaded.GetCheck(new CheckId("custom-5f4e3d2c1b0a")).Window);
        Assert.Equal(Domain.Templates.WeekWindow.OfWeek(23), loaded.GetCheck(new CheckId("custom-1b2c3d4e5f60")).Window);
        Assert.Equal(new Domain.Templates.WeekWindow(24, 26), loaded.GetCheck(new CheckId("custom-0a1b2c3d4e5f")).Window);
        Assert.Equal(Domain.Templates.WeekWindow.OfWeek(25), loaded.GetCheck(TestData.Checkup25).Window);
        Assert.Equal(new CheckId("custom-5f4e3d2c1b0a"), loaded.Checks[^1].Id);
    }

    [Fact]
    public async Task Upgrades_a_schema_2_plan_saved_before_the_next_visit_existed()
    {
        // A plan exactly as schema 2 stored it: questions, no next visit.
        var v2 = """
            {"schemaVersion":2,"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":5,"dueDate":"2027-01-17",
             "templateId":"test-gr","templateVersion":1,"language":"en","createdAt":"2026-09-30T09:00:00+03:00",
             "checks":[],"tasks":[],"contacts":[],
             "questions":[{"id":"3d0c4f0e-1b2a-4c5d-8e9f-0a1b2c3d4e5f","text":"Is swimming fine?","isAnswered":false}]}
            """u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName), await Cipher.EncryptAsync(v2, "plan", CancellationToken.None));

        var loaded = await Repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.DoctorVisits());
        Assert.Equal("Is swimming fine?", Assert.Single(loaded.Questions).Text);
    }

    [Fact]
    public async Task Upgrades_a_schema_1_plan_saved_before_questions_existed()
    {
        // A plan exactly as version 1 of the app stored it.
        var v1 = """
            {"schemaVersion":1,"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":3,"dueDate":"2027-01-17",
             "templateId":"test-gr","templateVersion":1,"language":"en","createdAt":"2026-09-30T09:00:00+03:00",
             "checks":[{"id":"checkup-25","origin":"Template","title":{"en":"Check-up","el":"Επίσκεψη"},
                        "category":"Routine","fromWeek":25,"toWeek":25,"state":"Booked",
                        "appointment":{"date":"2026-10-06","time":"10:30:00"},"results":[]}],
             "tasks":[{"id":"0b8f0c55-4a3e-4d8a-9a57-2d3b0c3f1e21","title":"Call clinic","dueDate":"2026-10-01","isDone":false}],
             "contacts":[]}
            """u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName), await Cipher.EncryptAsync(v1, "plan", CancellationToken.None));

        var loaded = await Repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Questions);
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)), loaded.GetCheck(TestData.Checkup25).Appointment);
        Assert.Equal("Call clinic", Assert.Single(loaded.Tasks).Title);

        // Saving writes the current schema, and it loads again.
        loaded.AddQuestion("Is swimming fine?");
        await Repository.SaveAsync(loaded, CancellationToken.None);
        Assert.Single((await Repository.LoadAsync(CancellationToken.None))!.Questions);
    }

    [Fact]
    public async Task Nothing_readable_is_written_to_disk()
    {
        await Repository.SaveAsync(RichPlan(), CancellationToken.None);

        var raw = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName)));
        Assert.DoesNotContain("Papadaki", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("checkup-25", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_the_backup_when_the_main_file_is_corrupt()
    {
        var plan = RichPlan();
        await Repository.SaveAsync(plan, CancellationToken.None);
        plan.AddTask("Second save", week: 30);
        await Repository.SaveAsync(plan, CancellationToken.None);

        var main = Path.Combine(Folder, EncryptedPlanRepository.FileName);
        var bytes = await File.ReadAllBytesAsync(main);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(main, bytes);

        var repository = Repository;
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.DoesNotContain(loaded.Tasks, t => t.Title == "Second save");
        Assert.True(repository.LastLoadUsedPreviousCopy);
    }

    [Fact]
    public async Task A_clean_load_is_not_marked_as_coming_from_the_previous_copy()
    {
        var repository = Repository;
        await repository.SaveAsync(RichPlan(), CancellationToken.None);
        await repository.SaveAsync(RichPlan(), CancellationToken.None);

        Assert.NotNull(await repository.LoadAsync(CancellationToken.None));
        Assert.False(repository.LastLoadUsedPreviousCopy);
    }

    [Fact]
    public async Task Refuses_plans_saved_by_a_newer_app_version()
    {
        var json = """{"schemaVersion":99,"id":"00000000-0000-0000-0000-000000000001"}"""u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName), await Cipher.EncryptAsync(json, "plan", CancellationToken.None));

        var ex = await Assert.ThrowsAsync<PlanUnreadableException>(() => Repository.LoadAsync(CancellationToken.None));
        Assert.True(ex.IsFromNewerApp);
        Assert.Equal(PlanUnreadableException.NewerAppCode, ex.Code);
    }

    [Fact]
    public async Task Delete_removes_plan_and_backup()
    {
        var plan = RichPlan();
        await Repository.SaveAsync(plan, CancellationToken.None);
        await Repository.SaveAsync(plan, CancellationToken.None);

        await Repository.DeleteAsync(CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(Folder));
    }

    [Fact]
    public async Task Delete_also_removes_what_an_interrupted_save_left_behind()
    {
        await Repository.SaveAsync(RichPlan(), CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(Folder, $"{EncryptedPlanRepository.FileName}.{Guid.NewGuid():N}.tmp"), [1, 2, 3]);

        await Repository.DeleteAsync(CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(Folder));
    }
}

public sealed class EncryptedAttachmentStoreTests : TempFolderTest
{
    private EncryptedAttachmentStore Store => new(Path.Combine(Folder, "results"), Cipher);

    [Fact]
    public async Task Round_trips_a_file_without_revealing_it()
    {
        var content = Encoding.UTF8.GetBytes("%PDF-1.7 anomaly scan report");

        var blobId = await Store.SaveAsync(content, CancellationToken.None);

        Assert.Equal(content, await Store.ReadAsync(blobId, CancellationToken.None));
        Assert.Equal(new[] { blobId }, Store.ListBlobIds());
        var raw = await File.ReadAllTextAsync(Path.Combine(Folder, "results", blobId + ".blob"));
        Assert.DoesNotContain("anomaly", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_is_idempotent()
    {
        var blobId = await Store.SaveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        await Store.DeleteAsync(blobId, CancellationToken.None);
        await Store.DeleteAsync(blobId, CancellationToken.None);

        Assert.Empty(Store.ListBlobIds());
    }

    [Fact]
    public async Task Delete_all_empties_the_folder_and_works_when_it_is_missing()
    {
        await Store.DeleteAllAsync(CancellationToken.None);
        await Store.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        await Store.SaveAsync(new byte[] { 2 }, CancellationToken.None);

        await Store.DeleteAllAsync(CancellationToken.None);

        Assert.Empty(Store.ListBlobIds());
    }

    [Fact]
    public async Task A_saved_file_is_complete_under_its_id_with_nothing_else_left_behind()
    {
        var blobId = await Store.SaveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(new[] { blobId + ".blob" }, Directory.EnumerateFiles(Path.Combine(Folder, "results")).Select(Path.GetFileName));
        Assert.True(EncryptedAttachmentStore.HasStoredFiles(Path.Combine(Folder, "results")));
    }

    [Fact]
    public async Task Delete_all_also_removes_a_file_a_crash_left_half_written()
    {
        await Store.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(Folder, "results", Guid.NewGuid().ToString("N") + ".tmp"), [1, 2]);

        await Store.DeleteAllAsync(CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(Folder, "results")));
        Assert.False(EncryptedAttachmentStore.HasStoredFiles(Path.Combine(Folder, "results")));
    }

    [Fact]
    public async Task Lists_stored_files_with_when_they_were_written()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var blobId = await Store.SaveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Folder, "results", "not-ours.blob"), "x");

        var blob = Assert.Single(await Store.ListAsync(CancellationToken.None));

        Assert.Equal(blobId, blob.BlobId);
        Assert.InRange(blob.SavedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Deleting_works_before_any_draft_was_ever_kept()
    {
        var store = new EncryptedDraftStore(Path.Combine(Folder, "never-made"), Cipher);

        await store.DeleteAsync("setup", CancellationToken.None);

        Assert.Null(await store.ReadAsync("setup", CancellationToken.None));
    }

    [Theory]
    [InlineData("../plan")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("short")]
    public async Task Rejects_blob_ids_it_did_not_create(string blobId) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Store.ReadAsync(blobId, CancellationToken.None));
}

public sealed class DraftStoreTests : TempFolderTest
{
    private EncryptedDraftStore Store(AesGcmCipher? cipher = null) => new(Folder, cipher ?? Cipher);

    [Fact]
    public async Task Keeps_a_draft_across_instances_without_revealing_it()
    {
        await Store().WriteAsync("setup", Encoding.UTF8.GetBytes("""{"dueDate":"2027-01-17","contact":"Dr. Eleni"}"""), CancellationToken.None);

        var read = await Store().ReadAsync("setup", CancellationToken.None);

        Assert.Equal("""{"dueDate":"2027-01-17","contact":"Dr. Eleni"}""", Encoding.UTF8.GetString(read!));
        var file = Assert.Single(Directory.EnumerateFiles(Folder));
        Assert.EndsWith("setup.maia", file, StringComparison.Ordinal);
        Assert.DoesNotContain("Eleni", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_damaged_or_foreign_draft_reads_as_none()
    {
        var store = Store();
        Assert.Null(await store.ReadAsync("setup", CancellationToken.None));

        await store.WriteAsync("setup", "{}"u8.ToArray(), CancellationToken.None);
        var otherKey = new AesGcmCipher(new InMemoryKeyProvider(AesGcmCipher.GenerateKey()));
        Assert.Null(await Store(otherKey).ReadAsync("setup", CancellationToken.None));

        await File.WriteAllBytesAsync(Path.Combine(Folder, "setup.maia"), [1, 2, 3]);
        Assert.Null(await store.ReadAsync("setup", CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_a_draft_removes_its_file_and_works_when_there_is_none()
    {
        var store = Store();
        await store.WriteAsync("setup", "{}"u8.ToArray(), CancellationToken.None);

        await store.DeleteAsync("setup", CancellationToken.None);
        await store.DeleteAsync("setup", CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(Folder));
        Assert.Null(await store.ReadAsync("setup", CancellationToken.None));
    }

    [Theory]
    [InlineData("../plan")]
    [InlineData("Setup")]
    [InlineData("")]
    public async Task Refuses_names_that_are_not_plain(string name) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Store().ReadAsync(name, CancellationToken.None));
}
