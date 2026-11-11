using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maia.Application.Common;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Infrastructure.Persistence;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

/// <summary>
/// Backups that are well encrypted under her password but hold something the app never writes: a record of an unknown
/// kind or size, no plan, a plan that is not one, or records cut across the chunks in ways a damaged file could be.
/// They are built here byte by byte, as ADR 0005 describes the file, so each one opens as far as the damage.
/// </summary>
public sealed class BackupFormatDamageTests
{
    private const string Password = "θάλασσα-17";
    private const byte EndRecord = 0;
    private const byte FileRecord = 1;
    private const byte PlanRecord = 2;

    /// <summary>"MAIABAK", the version, the key-derivation count, the salt and the nonce prefix.</summary>
    private const int HeaderSize = 7 + 1 + 4 + 16 + 8;

    private readonly EncryptedBackupFormat _format = new(EncryptedBackupFormat.MinIterations);
    private readonly List<byte[]> _saved = [];

    [Fact]
    public async Task A_backup_built_here_opens_as_one_the_app_wrote()
    {
        var plan = TestData.Plan();

        var restored = await RestoreAsync(Forge(Final([.. Record(PlanRecord, "plan", PlanJson(plan)), .. End()])));

        Assert.Equal((plan.Id, plan.Dating.DueDate), (restored.Id, restored.Dating.DueDate));
    }

    [Fact]
    public async Task A_record_of_a_kind_the_app_never_writes_is_damaged() =>
        await AssertDamagedAsync(Forge(Final([.. Record(9, "x", []), .. Record(PlanRecord, "plan", PlanJson(TestData.Plan())), .. End()])));

    [Theory]
    [InlineData(FileRecord, (64L * 1024 * 1024) + 1)]
    [InlineData(PlanRecord, (16L * 1024 * 1024) + 1)]
    [InlineData(FileRecord, -1L)]
    public async Task A_record_said_to_be_larger_than_its_kind_allows_or_negative_is_damaged_before_it_is_read(byte kind, long length)
    {
        var head = RecordHead(kind, "x", length);

        await AssertDamagedAsync(Forge(Final(head)));
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task A_backup_without_a_plan_is_damaged() =>
        await AssertDamagedAsync(Forge(Final([.. Record(FileRecord, "a", [1, 2, 3]), .. End()])));

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not a plan")]
    [InlineData("""{"schemaVersion":1,"id":"not a guid"}""")]
    public async Task A_plan_that_is_not_a_plan_is_damaged(string json) =>
        await AssertDamagedAsync(Forge(Final([.. Record(PlanRecord, "plan", Encoding.UTF8.GetBytes(json)), .. End()])));

    [Fact]
    public async Task A_plan_from_a_newer_app_inside_a_backup_is_refused()
    {
        var json = Encoding.UTF8.GetBytes($$"""{"schemaVersion":{{PlanDocument.CurrentSchemaVersion + 1}}}""");

        var error = await Assert.ThrowsAsync<BackupUnreadableException>(() => RestoreAsync(Forge(Final([.. Record(PlanRecord, "plan", json), .. End()]))));

        Assert.Equal(BackupUnreadableException.NewerAppCode, error.Code);
    }

    [Fact]
    public async Task A_plan_without_a_version_in_a_backup_is_read_as_the_first_schema()
    {
        var v1 = """
            {"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10","revision":3,"dueDate":"2027-01-17",
             "templateId":"test-gr","templateVersion":1,"language":"en","createdAt":"2026-09-30T09:00:00+03:00",
             "checks":[],"tasks":[],"contacts":[]}
            """u8.ToArray();

        var restored = await RestoreAsync(Forge(Final([.. Record(PlanRecord, "plan", v1), .. End()])));

        Assert.Equal((new DateOnly(2027, 1, 17), 3L), (restored.Dating.DueDate, restored.Revision));
        Assert.Empty(restored.Questions);
    }

    [Fact]
    public async Task A_result_whose_file_is_not_in_the_backup_is_damaged()
    {
        var plan = TestData.Plan();
        plan.AttachResult(ResultAttachment.ForFile(
            AttachmentId.New(), "report.pdf", "application/pdf", 3, "0123456789abcdef0123456789abcdef", DateTimeOffset.UnixEpoch));

        await AssertDamagedAsync(Forge(Final([.. Record(PlanRecord, "plan", PlanJson(plan)), .. End()])));
    }

    [Fact]
    public async Task A_record_that_runs_past_the_last_chunk_is_damaged()
    {
        // The head says 10 bytes; the last chunk holds 3 of them.
        await AssertDamagedAsync(Forge(Final([.. RecordHead(FileRecord, "a", 10), 1, 2, 3])));
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task An_end_record_that_fills_its_chunk_exactly_is_followed_by_an_empty_last_chunk_and_opens()
    {
        var plan = TestData.Plan();

        var restored = await RestoreAsync(Forge(
            NotFinal(Record(FileRecord, "unused", [7, 7, 7])),
            NotFinal([.. Record(PlanRecord, "plan", PlanJson(plan)), .. End()]),
            Final([])));

        Assert.Equal(plan.Id, restored.Id);
        Assert.Equal(new byte[] { 7, 7, 7 }, Assert.Single(_saved));
    }

    [Fact]
    public async Task Anything_after_the_end_record_is_damaged() =>
        await AssertDamagedAsync(Forge(Final([.. Record(PlanRecord, "plan", PlanJson(TestData.Plan())), .. End(), 0])));

    [Fact]
    public async Task A_backup_cut_inside_a_chunk_head_is_damaged()
    {
        var bytes = Forge(NotFinal(Record(PlanRecord, "plan", PlanJson(TestData.Plan()))), Final(End()));
        var secondChunk = bytes.Length - (5 + End().Length + 16);

        await AssertDamagedAsync(bytes[..(secondChunk + 3)]);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(EncryptedBackupFormat.ChunkSize + 1, 1)]
    [InlineData(0, 2)]
    public async Task A_chunk_head_no_app_writes_is_damaged(int length, byte final)
    {
        var bytes = Forge(Final([.. Record(PlanRecord, "plan", PlanJson(TestData.Plan())), .. End()]));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(HeaderSize), length);
        bytes[HeaderSize + 4] = final;

        await AssertDamagedAsync(bytes);
    }

    [Fact]
    public async Task A_backup_of_a_version_before_the_first_is_damaged()
    {
        var bytes = Forge(Final([.. Record(PlanRecord, "plan", PlanJson(TestData.Plan())), .. End()]));
        bytes[7] = 0;

        await AssertDamagedAsync(bytes);
    }

    [Fact]
    public async Task A_backup_written_by_the_app_uses_its_full_key_derivation_count_and_opens()
    {
        var format = new EncryptedBackupFormat();
        var plan = TestData.Plan();
        using var output = new MemoryStream();

        await format.WriteAsync(output, plan, _ => Task.FromResult<byte[]?>(null), Password, CancellationToken.None);
        var bytes = output.ToArray();
        var restored = await format.ReadAsync(new MemoryStream(bytes), Password, SaveAsync, CancellationToken.None);

        Assert.Equal(EncryptedBackupFormat.DefaultIterations, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8)));
        Assert.Equal(plan.Id, restored.Id);
    }

    private static byte[] PlanJson(PregnancyPlan plan) =>
        JsonSerializer.SerializeToUtf8Bytes(PlanDocumentMapper.ToDocument(plan), PersistenceJsonContext.Default.PlanDocument);

    private static byte[] RecordHead(byte kind, string name, long length)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var head = new byte[1 + 2 + nameBytes.Length + 8];
        head[0] = kind;
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(1), (ushort)nameBytes.Length);
        nameBytes.CopyTo(head, 3);
        BinaryPrimitives.WriteInt64BigEndian(head.AsSpan(3 + nameBytes.Length), length);
        return head;
    }

    private static byte[] Record(byte kind, string name, byte[] payload) => [.. RecordHead(kind, name, payload.Length), .. payload];

    private static byte[] End() => RecordHead(EndRecord, string.Empty, 0);

    private static (byte[] Plain, bool Final) Final(byte[] plain) => (plain, true);

    private static (byte[] Plain, bool Final) NotFinal(byte[] plain) => (plain, false);

    /// <summary>
    /// A backup under <see cref="Password"/>: the header, then each chunk as <c>length · final · ciphertext · tag</c>,
    /// its nonce the file's prefix and the chunk number, its associated data the header, the number and the flag.
    /// </summary>
    private static byte[] Forge(params (byte[] Plain, bool Final)[] chunks)
    {
        var header = new byte[HeaderSize];
        "MAIABAK"u8.CopyTo(header);
        header[7] = 1;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), EncryptedBackupFormat.MinIterations);
        RandomNumberGenerator.Fill(header.AsSpan(12));
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(Password.Normalize(NormalizationForm.FormC)), header.AsSpan(12, 16), EncryptedBackupFormat.MinIterations, HashAlgorithmName.SHA256, 32);

        using var aes = new AesGcm(key, 16);
        using var output = new MemoryStream();
        output.Write(header);
        for (var counter = 0; counter < chunks.Length; counter++)
        {
            var (plain, final) = chunks[counter];
            var nonce = new byte[12];
            header.AsSpan(28, 8).CopyTo(nonce);
            BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(8), counter);
            byte[] associated = [.. header, 0, 0, 0, 0, final ? (byte)1 : (byte)0];
            BinaryPrimitives.WriteInt32BigEndian(associated.AsSpan(HeaderSize), counter);

            var frame = new byte[5 + plain.Length + 16];
            BinaryPrimitives.WriteInt32BigEndian(frame, plain.Length);
            frame[4] = final ? (byte)1 : (byte)0;
            aes.Encrypt(nonce, plain, frame.AsSpan(5, plain.Length), frame.AsSpan(5 + plain.Length), associated);
            output.Write(frame);
        }

        return output.ToArray();
    }

    private Task<PregnancyPlan> RestoreAsync(byte[] bytes) =>
        _format.ReadAsync(new MemoryStream(bytes), Password, SaveAsync, CancellationToken.None);

    private Task<string> SaveAsync(ReadOnlyMemory<byte> content)
    {
        _saved.Add(content.ToArray());
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    private async Task AssertDamagedAsync(byte[] bytes)
    {
        var error = await Assert.ThrowsAsync<BackupUnreadableException>(() => RestoreAsync(bytes));
        Assert.Equal(BackupUnreadableException.DamagedCode, error.Code);
    }
}
