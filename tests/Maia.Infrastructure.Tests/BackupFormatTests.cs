using System.Buffers.Binary;
using System.Text;
using Maia.Application.Common;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Infrastructure.Persistence;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

public sealed class BackupFormatTests
{
    private const string Password = "θάλασσα-17";

    /// <summary>"MAIABAK", the version, the key-derivation count, the salt and the nonce prefix.</summary>
    private const int HeaderSize = 7 + 1 + 4 + 16 + 8;
    private const int IterationsOffset = 8;

    /// <summary>A full chunk as written: its length (4), the final flag (1), the ciphertext and the tag (16).</summary>
    private const int FullFrame = 5 + EncryptedBackupFormat.ChunkSize + 16;
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly EncryptedBackupFormat _format = new(EncryptedBackupFormat.MinIterations);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _restored = new(StringComparer.Ordinal);

    [Fact]
    public async Task A_backup_round_trips_the_plan_and_files_under_new_ids()
    {
        var plan = PlanWithFiles(("report.pdf", 1_000), ("scan.jpg", 200_000));

        var restored = await RestoreAsync(await BackupAsync(plan));

        var results = restored.Results;
        Assert.Equal(new[] { "report.pdf", "scan.jpg" }, results.Select(r => r.DisplayName));
        Assert.All(results, r => Assert.DoesNotContain(r.BlobId!, _files.Keys));
        Assert.Equal(new[] { 1_000, 200_000 }, results.Select(r => _restored[r.BlobId!].Length));
        Assert.Equal(_files.Values.Select(v => v[^1]), results.Select(r => _restored[r.BlobId!][^1]));
        Assert.Equal(plan.Dating.DueDate, restored.Dating.DueDate);
    }

    [Fact]
    public async Task A_backup_keeps_results_with_their_files_and_what_her_entries_are()
    {
        var plan = PlanWithFiles();
        var blobId = Guid.NewGuid().ToString("N");
        _files[blobId] = Encoding.ASCII.GetBytes("bloods");
        var bloods = ResultAttachment.ForFile(AttachmentId.New(), "bloods.pdf", "application/pdf", 6, blobId, Now);
        plan.AttachResult(bloods);
        var visit = plan.AddEntry(LocalizedText.Plain("Dentist"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0))));

        var restored = await RestoreAsync(await BackupAsync(plan));

        var result = Assert.Single(restored.Results);
        Assert.Equal((bloods.Id, "bloods.pdf"), (result.Id, result.DisplayName));
        Assert.Equal("bloods"u8.ToArray(), _restored[result.BlobId!]);
        Assert.Equal(EntryKind.DoctorVisit, restored.GetCheck(visit).Kind);
    }

    [Fact]
    public async Task A_result_whose_file_is_gone_is_left_out_and_the_rest_restores()
    {
        var plan = PlanWithFiles(("report.pdf", 1_000));
        var gone = ResultAttachment.ForFile(AttachmentId.New(), "bloods.pdf", "application/pdf", 6, Guid.NewGuid().ToString("N"), Now);
        plan.AttachResult(gone);
        using var output = new MemoryStream();

        var skipped = await _format.WriteAsync(output, plan, blobId => Task.FromResult(_files.GetValueOrDefault(blobId)), Password, CancellationToken.None);
        var restored = await RestoreAsync(output.ToArray());

        Assert.Equal(gone.BlobId, Assert.Single(skipped));
        Assert.Equal("report.pdf", Assert.Single(restored.Results).DisplayName);
    }

    [Fact]
    public async Task A_backup_keeps_the_plans_home_time_zone()
    {
        var plan = PlanWithFiles();
        plan.SetHomeTimeZoneIfUnknown("Europe/Athens");

        var restored = await RestoreAsync(await BackupAsync(plan));

        Assert.Equal("Europe/Athens", restored.TimeZoneId);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_left_out_and_the_rest_restores()
    {
        var plan = PlanWithFiles(("report.pdf", 1_000), ("scan.jpg", 200_000));
        var unreadable = plan.Results[0].BlobId!;
        using var output = new MemoryStream();

        var skipped = await _format.WriteAsync(
            output, plan, blobId => Task.FromResult<byte[]?>(blobId == unreadable ? null : _files[blobId]), Password, CancellationToken.None);
        var restored = await RestoreAsync(output.ToArray());

        Assert.Equal(new[] { unreadable }, skipped);
        var result = Assert.Single(restored.Results);
        Assert.Equal("scan.jpg", result.DisplayName);
        Assert.Equal(200_000, _restored[result.BlobId!].Length);
    }

    [Fact]
    public async Task Nothing_readable_is_in_the_file()
    {
        var bytes = await BackupAsync(PlanWithFiles(("report.pdf", 1_000)));
        var text = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("report.pdf", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Glucose", text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('r', 100), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_password_typed_with_other_accent_keys_still_opens_it()
    {
        var bytes = await BackupAsync(PlanWithFiles());

        // "ά" as one character when writing, as "α" plus a combining accent when reading.
        var restored = await _format.ReadAsync(new MemoryStream(bytes), Password.Normalize(NormalizationForm.FormD), SaveAsync, CancellationToken.None);

        Assert.NotNull(restored);
    }

    [Fact]
    public async Task A_wrong_password_says_so() =>
        await AssertUnreadableAsync(await BackupAsync(PlanWithFiles()), BackupUnreadableException.WrongPasswordCode, "θάλασσα-18");

    [Fact]
    public async Task Other_files_are_not_backups() =>
        await AssertUnreadableAsync(Encoding.UTF8.GetBytes("MAIA not a backup at all"), BackupUnreadableException.NotABackupCode);

    [Fact]
    public async Task A_cut_off_backup_is_damaged()
    {
        var bytes = await BackupAsync(PlanWithFiles(("scan.jpg", 200_000)));

        await AssertUnreadableAsync(bytes[..^40], BackupUnreadableException.DamagedCode);
    }

    [Fact]
    public async Task A_backup_with_extra_bytes_is_damaged()
    {
        var bytes = await BackupAsync(PlanWithFiles());

        await AssertUnreadableAsync([.. bytes, 0], BackupUnreadableException.DamagedCode);
    }

    [Fact]
    public async Task A_changed_byte_is_detected()
    {
        var bytes = await BackupAsync(PlanWithFiles(("scan.jpg", 200_000)));
        bytes[^100] ^= 0x01;

        await AssertUnreadableAsync(bytes, BackupUnreadableException.DamagedCode);
    }

    [Fact]
    public async Task Two_whole_chunks_swapped_in_a_large_backup_are_detected()
    {
        var bytes = await BackupAsync(PlanWithFiles(("scan.jpg", 200_000)));
        Assert.True(bytes.Length > 3 * FullFrame + HeaderSize);
        var second = Frame(bytes, 1);
        var third = Frame(bytes, 2);
        Assert.Equal(second.Length, third.Length);

        // Each chunk is intact on its own; only their order is wrong.
        third.CopyTo(bytes, HeaderSize + FullFrame);
        second.CopyTo(bytes, HeaderSize + (2 * FullFrame));

        await AssertUnreadableAsync(bytes, BackupUnreadableException.DamagedCode);
        Assert.Empty(_restored);
    }

    [Fact]
    public async Task A_chunk_left_out_of_the_middle_is_detected()
    {
        var bytes = await BackupAsync(PlanWithFiles(("scan.jpg", 200_000)));

        byte[] shortened = [.. bytes[..(HeaderSize + FullFrame)], .. bytes[(HeaderSize + (2 * FullFrame))..]];

        await AssertUnreadableAsync(shortened, BackupUnreadableException.DamagedCode);
    }

    [Fact]
    public async Task The_first_chunk_moved_elsewhere_never_opens()
    {
        var bytes = await BackupAsync(PlanWithFiles(("scan.jpg", 200_000)));
        var first = Frame(bytes, 0);
        var second = Frame(bytes, 1);

        second.CopyTo(bytes, HeaderSize);
        first.CopyTo(bytes, HeaderSize + FullFrame);

        await Assert.ThrowsAsync<BackupUnreadableException>(() => _format.ReadAsync(new MemoryStream(bytes), Password, SaveAsync, CancellationToken.None));
        Assert.Empty(_restored);
    }

    [Theory]
    [InlineData(EncryptedBackupFormat.MinIterations - 1)]
    [InlineData(EncryptedBackupFormat.MaxIterations + 1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task A_key_derivation_count_outside_the_bounds_is_damaged_and_never_run(int iterations)
    {
        var bytes = await BackupAsync(PlanWithFiles());
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(IterationsOffset), iterations);

        // int.MaxValue rounds of PBKDF2 would take hours: the bounds are checked before any key is derived.
        await AssertUnreadableAsync(bytes, BackupUnreadableException.DamagedCode).WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_key_derivation_count_changed_within_the_bounds_does_not_open_it()
    {
        var bytes = await BackupAsync(PlanWithFiles());
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(IterationsOffset), EncryptedBackupFormat.MinIterations + 1);

        await AssertUnreadableAsync(bytes, BackupUnreadableException.WrongPasswordCode);
    }

    [Fact]
    public void Fewer_rounds_than_the_minimum_are_refused_when_writing() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncryptedBackupFormat(EncryptedBackupFormat.MinIterations - 1));

    [Fact]
    public async Task A_backup_from_a_newer_app_is_refused()
    {
        var bytes = await BackupAsync(PlanWithFiles());
        bytes[7] = 2;

        await AssertUnreadableAsync(bytes, BackupUnreadableException.NewerAppCode);
    }

    private static byte[] Frame(byte[] bytes, int index) => bytes.AsSpan(HeaderSize + (index * FullFrame), FullFrame).ToArray();

    private PregnancyPlan PlanWithFiles(params (string Name, int Size)[] files)
    {
        var plan = TestData.Plan();
        foreach (var (name, size) in files)
        {
            var blobId = Guid.NewGuid().ToString("N");
            var content = Encoding.ASCII.GetBytes(new string('r', size - 1) + name[0]);
            _files[blobId] = content;
            var mediaType = name.EndsWith(".jpg", StringComparison.Ordinal) ? "image/jpeg" : "application/pdf";
            plan.AttachResult(ResultAttachment.ForFile(AttachmentId.New(), name, mediaType, size, blobId, Now));
        }

        return plan;
    }

    private async Task<byte[]> BackupAsync(PregnancyPlan plan)
    {
        using var output = new MemoryStream();
        await _format.WriteAsync(output, plan, blobId => Task.FromResult<byte[]?>(_files[blobId]), Password, CancellationToken.None);
        return output.ToArray();
    }

    private Task<PregnancyPlan> RestoreAsync(byte[] bytes) =>
        _format.ReadAsync(new MemoryStream(bytes), Password, SaveAsync, CancellationToken.None);

    private Task<string> SaveAsync(ReadOnlyMemory<byte> content)
    {
        var blobId = Guid.NewGuid().ToString("N");
        _restored[blobId] = content.ToArray();
        return Task.FromResult(blobId);
    }

    private async Task AssertUnreadableAsync(byte[] bytes, string code, string password = Password)
    {
        var error = await Assert.ThrowsAsync<BackupUnreadableException>(
            () => _format.ReadAsync(new MemoryStream(bytes), password, SaveAsync, CancellationToken.None));
        Assert.Equal(code, error.Code);
    }
}
