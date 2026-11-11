using System.Text;
using System.Text.Json.Nodes;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Templates;
using Maia.Infrastructure.Attachments;
using Maia.Infrastructure.Persistence;
using Maia.Infrastructure.Security;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

/// <summary>
/// Where her plan is most at risk on disk: a damaged file, a plan from a newer Maia, a save cut short, and a stored
/// record the domain has to repair. Real encryption and real files, as on the phone.
/// </summary>
public sealed class PlanStorageRecoveryTests : TempFolderTest
{
    private readonly RecordingErrorReporter _errors = new();

    private string MainPath => Path.Combine(Folder, EncryptedPlanRepository.FileName);

    private string BackupPath => Path.Combine(Folder, EncryptedPlanRepository.BackupFileName);

    private EncryptedPlanRepository Repository() => new(Folder, Cipher, _errors);

    private static async Task CorruptAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(path, bytes);
    }

    /// <summary>Two saves: the backup copy holds the plan before "Second save", the main file the plan with it.</summary>
    private static async Task SaveTwiceAsync(EncryptedPlanRepository repository)
    {
        var plan = TestData.Plan();
        await repository.SaveAsync(plan, CancellationToken.None);
        plan.AddTask("Second save", week: 30);
        await repository.SaveAsync(plan, CancellationToken.None);
    }

    private async Task<JsonObject> ReadJsonAsync(string path)
    {
        var json = await Cipher.DecryptAsync(await File.ReadAllBytesAsync(path), "plan", CancellationToken.None);
        return (JsonObject)JsonNode.Parse(json)!;
    }

    private async Task WriteJsonAsync(string path, JsonNode json) =>
        await File.WriteAllBytesAsync(path, await Cipher.EncryptAsync(Encoding.UTF8.GetBytes(json.ToJsonString()), "plan", CancellationToken.None));

    private static JsonObject CheckIn(JsonObject plan, CheckId id) =>
        plan["checks"]!.AsArray().OfType<JsonObject>().Single(c => (string?)c["id"] == id.Value);

    [Fact]
    public async Task A_damaged_main_file_falls_back_to_the_backup_copy_and_says_so_once()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        await CorruptAsync(MainPath);

        var loaded = await repository.LoadAsync(CancellationToken.None);
        await repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.DoesNotContain(loaded.Tasks, t => t.Title == "Second save");
        Assert.Equal(EncryptedPlanRepository.FallbackOperation, Assert.Single(_errors.Reports).Operation);
    }

    [Fact]
    public async Task A_save_after_a_fallback_keeps_the_good_backup_copy()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        await CorruptAsync(MainPath);
        var plan = (await repository.LoadAsync(CancellationToken.None))!;
        plan.AddTask("After the fallback", week: 30);

        await repository.SaveAsync(plan, CancellationToken.None);

        Assert.Contains((await Repository().LoadAsync(CancellationToken.None))!.Tasks, t => t.Title == "After the fallback");

        // The damaged file never took the good copy's place: even if the new main file is lost too, a plan opens.
        await CorruptAsync(MainPath);
        Assert.NotNull(await Repository().LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Both_copies_damaged_is_an_unreadable_plan_not_a_crash()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        await CorruptAsync(MainPath);
        await CorruptAsync(BackupPath);

        var ex = await Assert.ThrowsAsync<PlanUnreadableException>(() => repository.LoadAsync(CancellationToken.None));

        Assert.Equal(PlanUnreadableException.DamagedCode, ex.Code);
        Assert.False(ex.IsFromNewerApp);
    }

    [Fact]
    public async Task Another_phone_s_key_opens_neither_copy()
    {
        await SaveTwiceAsync(Repository());
        var otherPhone = new EncryptedPlanRepository(Folder, new AesGcmCipher(new InMemoryKeyProvider(AesGcmCipher.GenerateKey())));

        var ex = await Assert.ThrowsAsync<PlanUnreadableException>(() => otherPhone.LoadAsync(CancellationToken.None));

        Assert.Equal(PlanUnreadableException.DamagedCode, ex.Code);
    }

    [Fact]
    public async Task A_plan_from_a_newer_maia_is_refused_and_never_swapped_for_the_older_copy()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        var newer = await ReadJsonAsync(MainPath);
        newer["schemaVersion"] = PlanDocument.CurrentSchemaVersion + 1;
        newer["somethingNew"] = "a field this version doesn't know";
        await WriteJsonAsync(MainPath, newer);
        var main = await File.ReadAllBytesAsync(MainPath);
        var backup = await File.ReadAllBytesAsync(BackupPath);

        var ex = await Assert.ThrowsAsync<PlanUnreadableException>(() => repository.LoadAsync(CancellationToken.None));

        Assert.True(ex.IsFromNewerApp);
        Assert.Equal(main, await File.ReadAllBytesAsync(MainPath));
        Assert.Equal(backup, await File.ReadAllBytesAsync(BackupPath));
        Assert.Empty(_errors.Reports);
    }

    [Fact]
    public async Task A_save_cut_short_before_its_rename_leaves_the_plan_as_it_was()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);

        // The phone died while the next save was writing its temp file.
        var leftover = Path.Combine(Folder, $"{EncryptedPlanRepository.FileName}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(leftover, [0x4D, 0x41]);

        var restarted = Repository();
        var plan = await restarted.LoadAsync(CancellationToken.None);

        Assert.Contains(plan!.Tasks, t => t.Title == "Second save");
        Assert.Empty(_errors.Reports);
        plan.AddTask("Third save", week: 30);
        await restarted.SaveAsync(plan, CancellationToken.None);
        Assert.Contains((await Repository().LoadAsync(CancellationToken.None))!.Tasks, t => t.Title == "Third save");
    }

    [Fact]
    public async Task A_save_cut_short_between_its_renames_opens_the_backup_copy()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        File.Delete(MainPath);

        var plan = await Repository().LoadAsync(CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Equal(EncryptedPlanRepository.FallbackOperation, Assert.Single(_errors.Reports).Operation);
    }

    [Fact]
    public async Task Saves_leave_only_the_plan_and_its_backup_copy()
    {
        await SaveTwiceAsync(Repository());

        Assert.Equal(
            new[] { EncryptedPlanRepository.FileName, EncryptedPlanRepository.BackupFileName }.Order(),
            Directory.EnumerateFiles(Folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task A_record_the_plan_cannot_take_falls_back_to_the_backup_copy()
    {
        var repository = Repository();
        await SaveTwiceAsync(repository);
        var broken = await ReadJsonAsync(MainPath);
        CheckIn(broken, TestData.Glucose)["title"] = new JsonObject { ["el"] = "Καμπύλη σακχάρου" };
        await WriteJsonAsync(MainPath, broken);

        var plan = await Repository().LoadAsync(CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Equal(EncryptedPlanRepository.FallbackOperation, Assert.Single(_errors.Reports).Operation);
    }

    [Fact]
    public async Task A_stored_booking_without_its_date_opens_as_needing_a_booking_and_is_reported_by_code()
    {
        await Repository().SaveAsync(TestData.Plan(), CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        CheckIn(stored, TestData.Glucose)["state"] = nameof(CheckState.Booked);
        await WriteJsonAsync(MainPath, stored);

        var plan = await Repository().LoadAsync(CancellationToken.None);

        Assert.Equal(CheckState.NotBooked, plan!.GetCheck(TestData.Glucose).State);
        var report = Assert.Single(_errors.Reports);
        Assert.Equal(EncryptedPlanRepository.RepairOperationPrefix + "check.booked_without_appointment", report.Operation);
        Assert.DoesNotContain("Glucose", report.Exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_check_stored_as_pending_opens_as_not_booked_and_is_saved_with_that_name()
    {
        await Repository().SaveAsync(TestData.Plan(), CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        CheckIn(stored, TestData.Glucose)["state"] = "Pending";
        CheckIn(stored, TestData.Glucose)["completedOn"] = "2026-09-02";
        await WriteJsonAsync(MainPath, stored);

        var plan = await Repository().LoadAsync(CancellationToken.None);
        await Repository().SaveAsync(plan!, CancellationToken.None);

        var glucose = plan!.GetCheck(TestData.Glucose);
        Assert.Equal(CheckState.NotBooked, glucose.State);
        Assert.Null(glucose.Appointment);
        Assert.Null(glucose.TrimesterOnly);
        Assert.Empty(_errors.Reports);
        var saved = CheckIn(await ReadJsonAsync(MainPath), TestData.Glucose);
        Assert.Equal(nameof(CheckState.NotBooked), (string?)saved["state"]);
        Assert.Null(saved["completedOn"]);
    }

    [Fact]
    public async Task A_check_stored_as_done_with_its_booking_keeps_the_booking_and_shows_done_once_it_has_passed()
    {
        await Repository().SaveAsync(TestData.Plan(), CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        CheckIn(stored, TestData.Glucose)["state"] = nameof(CheckState.Done);
        CheckIn(stored, TestData.Glucose)["appointment"] = JsonNode.Parse("""{"date":"2026-09-20","time":"10:00:00","location":"Clinic"}""");
        CheckIn(stored, TestData.Glucose)["completedOn"] = "2026-09-20";
        await WriteJsonAsync(MainPath, stored);

        var plan = (await Repository().LoadAsync(CancellationToken.None))!;

        var glucose = plan.GetCheck(TestData.Glucose);
        Assert.Equal(CheckState.Booked, glucose.State);
        Assert.Equal(new Appointment(new DateOnly(2026, 9, 20), new TimeOnly(10, 0), "Clinic"), glucose.Appointment);
        Assert.Equal(CheckStatus.Done, plan.StatusOf(glucose, TestData.Today, new TimeOnly(12, 0)));
        Assert.Empty(_errors.Reports);
    }

    [Fact]
    public async Task A_check_stored_as_not_needed_is_left_out_of_her_journey()
    {
        var saved = TestData.Plan();
        await Repository().SaveAsync(saved, CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        CheckIn(stored, TestData.Checkup25)["state"] = "NotNeeded";
        await WriteJsonAsync(MainPath, stored);

        var plan = await Repository().LoadAsync(CancellationToken.None);
        await Repository().SaveAsync(plan!, CancellationToken.None);

        Assert.DoesNotContain(plan!.Checks, c => c.Id == TestData.Checkup25);
        Assert.Equal(saved.Checks.Count - 1, plan.Checks.Count);
        Assert.Empty(_errors.Reports);
        Assert.DoesNotContain(
            (await ReadJsonAsync(MainPath))["checks"]!.AsArray().OfType<JsonObject>(),
            c => (string?)c["id"] == TestData.Checkup25.Value);
    }

    [Fact]
    public async Task A_current_plan_opens_wherever_its_version_stands_among_its_fields()
    {
        var saved = TestData.Plan();
        saved.AddTask("Call the clinic", week: 30);
        await Repository().SaveAsync(saved, CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        stored.Remove("schemaVersion");
        stored["schemaVersion"] = PlanDocument.CurrentSchemaVersion;
        await WriteJsonAsync(MainPath, stored);

        var plan = await Repository().LoadAsync(CancellationToken.None);

        Assert.Equal(saved.Id, plan!.Id);
        Assert.Equal("Call the clinic", Assert.Single(plan.Tasks).Title);
    }

    [Fact]
    public async Task A_plan_without_a_version_is_read_as_the_first_schema()
    {
        await Repository().SaveAsync(TestData.Plan(), CancellationToken.None);
        var stored = await ReadJsonAsync(MainPath);
        stored.Remove("schemaVersion");
        stored.Remove("questions");
        await WriteJsonAsync(MainPath, stored);

        var plan = await Repository().LoadAsync(CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Empty(plan.Questions);
    }
}

/// <summary>The data key in the phone's keystore: made once, never over files it can't open, and forgotten on request.</summary>
public sealed class DataKeyProviderTests
{
    private readonly InMemorySecretStore _keystore = new();
    private bool _hasFiles;

    private DataKeyProvider Provider() => new(_keystore, () => _hasFiles);

    [Fact]
    public async Task Makes_a_key_on_first_use_and_the_same_key_comes_back_after_a_restart()
    {
        byte[] first;
        using (var provider = Provider())
        {
            first = (await provider.GetKeyAsync(CancellationToken.None)).ToArray();
        }

        _hasFiles = true;
        using var restarted = Provider();

        Assert.Equal(AesGcmCipher.KeySizeBytes, first.Length);
        Assert.Equal(first, await restarted.GetKeyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Never_makes_a_new_key_over_files_it_could_not_open()
    {
        _hasFiles = true;
        using var provider = Provider();

        await Assert.ThrowsAsync<DataKeyUnavailableException>(() => provider.GetKeyAsync(CancellationToken.None).AsTask());

        Assert.Empty(_keystore.Values);
    }

    [Fact]
    public async Task A_keystore_that_cannot_be_read_means_the_key_is_unavailable()
    {
        _keystore.FailReads = new InvalidOperationException("keystore locked");
        using var provider = Provider();

        var ex = await Assert.ThrowsAsync<DataKeyUnavailableException>(() => provider.GetKeyAsync(CancellationToken.None).AsTask());

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task A_keystore_write_that_fails_means_the_key_is_unavailable()
    {
        _keystore.FailWrites = new InvalidOperationException("preferences could not be opened");
        using var provider = Provider();

        var ex = await Assert.ThrowsAsync<DataKeyUnavailableException>(() => provider.GetKeyAsync(CancellationToken.None).AsTask());

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task A_key_the_keystore_silently_drops_is_never_used()
    {
        // Nothing may be saved with a key that would be gone after a restart.
        _keystore.DropWrites = true;
        using var provider = Provider();

        await Assert.ThrowsAsync<DataKeyUnavailableException>(() => provider.GetKeyAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task A_stored_value_that_is_not_a_key_is_replaced_only_while_no_file_exists()
    {
        _keystore.Values[DataKeyProvider.KeyName] = "not a key";
        _hasFiles = true;
        using (var provider = Provider())
        {
            await Assert.ThrowsAsync<DataKeyUnavailableException>(() => provider.GetKeyAsync(CancellationToken.None).AsTask());
        }

        _hasFiles = false;
        using var fresh = Provider();
        var key = await fresh.GetKeyAsync(CancellationToken.None);

        Assert.Equal(Convert.ToBase64String(key), _keystore.Values[DataKeyProvider.KeyName]);
    }

    [Fact]
    public async Task Forgetting_the_key_removes_it_and_the_next_one_is_new()
    {
        using var provider = Provider();
        var first = (await provider.GetKeyAsync(CancellationToken.None)).ToArray();

        await provider.ForgetAsync(CancellationToken.None);

        Assert.Empty(_keystore.Values);
        var second = await provider.GetKeyAsync(CancellationToken.None);
        Assert.NotEqual(first, second);
        Assert.Equal(Convert.ToBase64String(second), _keystore.Values[DataKeyProvider.KeyName]);
    }
}

/// <summary>
/// Whole phones: the real repository, result store, key provider and plan service over a folder and a keystore that
/// outlive a restart. Covers moving phones, a lost key, and deleting everything.
/// </summary>
public sealed class PhoneStorageTests : TempFolderTest
{
    private const string Password = "sea-breeze-17";
    private static readonly FileToAttach Scan = new("20-week scan.pdf", "application/pdf", "%PDF-1.7 anomaly scan"u8.ToArray());

    private static readonly Appointment ABooking = new(new DateOnly(2026, 10, 12), new TimeOnly(9, 30));

    private readonly InMemorySecretStore _keystore = new();

    private TestPhone Phone() => new(Path.Combine(Folder, "phone"), _keystore);

    private Dictionary<string, byte[]> FilesOnThePhone()
    {
        var root = Path.Combine(Folder, "phone");
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(root, p), File.ReadAllBytes, StringComparer.Ordinal);
    }

    private static void AssertSameFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        Assert.All(expected, file => Assert.Equal(file.Value, actual[file.Key]));
    }

    /// <summary>A backup made on another phone (with its own key) holding a plan with one task.</summary>
    private async Task<byte[]> BackupFromAnotherPhoneAsync()
    {
        using var other = new TestPhone(Path.Combine(Folder, "other"), new InMemorySecretStore());
        await other.CreatePlanAsync();
        await other.Plans.AddTaskAsync("From the backup", week: 30);
        using var backup = new MemoryStream();
        await other.Plans.ExportBackupAsync(backup, Password);
        return backup.ToArray();
    }

    /// <summary>Her plan and one result file on this phone, then the key is gone (the files moved without it).</summary>
    private async Task LoseTheKeyToAPlanAsync()
    {
        using (var phone = Phone())
        {
            await phone.CreatePlanAsync();
            await phone.Plans.AttachFileAsync(Scan);
        }

        _keystore.Values.Clear();
    }

    [Fact]
    public async Task A_plan_made_after_deleting_everything_opens_after_a_restart()
    {
        string firstKey;
        using (var phone = Phone())
        {
            await phone.CreatePlanAsync();
            await phone.Plans.AttachFileAsync(Scan);
            firstKey = _keystore.Values[DataKeyProvider.KeyName];

            await phone.Plans.DeleteEverythingAsync();
            await phone.CreatePlanAsync();
        }

        using var restarted = Phone();

        Assert.NotNull(await restarted.Repository.LoadAsync(CancellationToken.None));
        Assert.NotEqual(firstKey, _keystore.Values[DataKeyProvider.KeyName]);
    }

    [Fact]
    public async Task Deleting_everything_leaves_no_file_and_no_key_that_could_open_one()
    {
        using var phone = Phone();
        await phone.CreatePlanAsync();
        await phone.Plans.AttachFileAsync(Scan);
        await phone.Plans.BookCheckAsync(TestData.Glucose, ABooking);
        await File.WriteAllBytesAsync(Path.Combine(phone.PlanFolder, $"{EncryptedPlanRepository.FileName}.{Guid.NewGuid():N}.tmp"), [1, 2, 3]);

        await phone.Plans.DeleteEverythingAsync();

        Assert.Empty(FilesOnThePhone());
        Assert.Empty(_keystore.Values);
    }

    [Fact]
    public async Task A_new_phone_restores_a_backup_made_under_another_key()
    {
        byte[] backup;
        using (var oldPhone = new TestPhone(Path.Combine(Folder, "old"), new InMemorySecretStore()))
        {
            await oldPhone.CreatePlanAsync();
            await oldPhone.Plans.AttachFileAsync(Scan);
            using var file = new MemoryStream();
            await oldPhone.Plans.ExportBackupAsync(file, Password);
            backup = file.ToArray();
        }

        using (var newPhone = Phone())
        {
            await newPhone.Plans.RestoreBackupAsync(new MemoryStream(backup), Password);
        }

        using var restarted = Phone();
        var plan = await restarted.Repository.LoadAsync(CancellationToken.None);
        var result = Assert.Single(plan!.Results);
        Assert.Equal(Scan.Content.ToArray(), await restarted.Plans.ReadResultFileAsync(result.Id));
    }

    [Fact]
    public async Task A_plan_whose_key_is_gone_is_left_exactly_as_it_is()
    {
        await LoseTheKeyToAPlanAsync();
        var before = FilesOnThePhone();
        using var restarted = Phone();

        var ex = await Assert.ThrowsAsync<PlanUnreadableException>(() => restarted.Repository.LoadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<PlanUnreadableException>(restarted.CreatePlanAsync);

        Assert.Equal(PlanUnreadableException.KeyUnavailableCode, ex.Code);
        Assert.Empty(_keystore.Values);
        AssertSameFiles(before, FilesOnThePhone());
    }

    [Fact]
    public async Task Restoring_a_backup_replaces_a_plan_whose_key_is_gone()
    {
        var backup = await BackupFromAnotherPhoneAsync();
        await LoseTheKeyToAPlanAsync();

        using (var phone = Phone())
        {
            await phone.Plans.RestoreBackupAsync(new MemoryStream(backup), Password);
        }

        using var restarted = Phone();
        var plan = await restarted.Repository.LoadAsync(CancellationToken.None);
        Assert.Equal("From the backup", Assert.Single(plan!.Tasks).Title);
        Assert.Empty(Directory.EnumerateFiles(restarted.ResultsFolder));
    }

    [Fact]
    public async Task A_wrong_password_leaves_the_plan_it_cannot_open_in_place()
    {
        var backup = await BackupFromAnotherPhoneAsync();
        await LoseTheKeyToAPlanAsync();
        var before = FilesOnThePhone();
        using var phone = Phone();

        await Assert.ThrowsAsync<BackupUnreadableException>(() => phone.Plans.RestoreBackupAsync(new MemoryStream(backup), "sea-breeze-18"));

        AssertSameFiles(before, FilesOnThePhone());
        Assert.Empty(_keystore.Values);
    }

    [Fact]
    public async Task A_plan_from_a_newer_maia_is_never_replaced_or_written_over()
    {
        var backup = await BackupFromAnotherPhoneAsync();
        using (var phone = Phone())
        {
            await phone.CreatePlanAsync();
            await phone.Plans.BookCheckAsync(TestData.Glucose, ABooking);
            var main = Path.Combine(phone.PlanFolder, EncryptedPlanRepository.FileName);
            var json = (JsonObject)JsonNode.Parse(await phone.Cipher.DecryptAsync(await File.ReadAllBytesAsync(main), "plan", CancellationToken.None))!;
            json["schemaVersion"] = PlanDocument.CurrentSchemaVersion + 1;
            await File.WriteAllBytesAsync(main, await phone.Cipher.EncryptAsync(Encoding.UTF8.GetBytes(json.ToJsonString()), "plan", CancellationToken.None));
        }

        var before = FilesOnThePhone();
        using var restarted = Phone();

        var restore = await Assert.ThrowsAsync<PlanUnreadableException>(() => restarted.Plans.RestoreBackupAsync(new MemoryStream(backup), Password));
        await Assert.ThrowsAsync<PlanUnreadableException>(() => restarted.Plans.BookCheckAsync(TestData.Glucose, ABooking));
        await Assert.ThrowsAsync<PlanUnreadableException>(restarted.CreatePlanAsync);

        Assert.True(restore.IsFromNewerApp);
        AssertSameFiles(before, FilesOnThePhone());
    }
}

/// <summary>Maia's storage and plan service on one phone. Its folder and keystore outlive it, as they outlive a restart.</summary>
internal sealed class TestPhone : IDisposable
{
    /// <param name="folder">Maia's data folder.</param>
    /// <param name="keystore">The phone's keystore.</param>
    public TestPhone(string folder, InMemorySecretStore keystore)
    {
        PlanFolder = Path.Combine(folder, "plan");
        ResultsFolder = Path.Combine(folder, "results");
        Keys = new DataKeyProvider(keystore, () => EncryptedPlanRepository.HasStoredPlan(PlanFolder) || EncryptedAttachmentStore.HasStoredFiles(ResultsFolder));
        Cipher = new AesGcmCipher(Keys);
        Repository = new EncryptedPlanRepository(PlanFolder, Cipher, Errors);
        var attachments = new EncryptedAttachmentStore(ResultsFolder, Cipher);
        var clock = new FakeClock(TestData.Today);
        Plans = new PlanService(Repository, new TestTemplates(), attachments, Keys, new InMemoryBackupFormat(), clock, Errors);
    }

    public string PlanFolder { get; }

    public string ResultsFolder { get; }

    public DataKeyProvider Keys { get; }

    public AesGcmCipher Cipher { get; }

    public EncryptedPlanRepository Repository { get; }

    public PlanService Plans { get; }

    public RecordingErrorReporter Errors { get; } = new();

    public Task CreatePlanAsync() => Plans.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "GR", "en"));

    public void Dispose()
    {
        Plans.Dispose();
        Keys.Dispose();
    }

    private sealed class TestTemplates : IPlanTemplateCatalog
    {
        public IReadOnlyList<string> SupportedCountries => ["GR"];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(TestData.Template());
    }
}
