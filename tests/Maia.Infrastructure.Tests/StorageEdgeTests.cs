using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Maia.Application.Common;
using Maia.Infrastructure.Attachments;
using Maia.Infrastructure.Persistence;
using Maia.Infrastructure.Security;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

/// <summary>Saves and loads that go wrong in the ways the phone allows: a file in the way, a keystore shut down, a stored value no Maia wrote.</summary>
public sealed class PlanStorageEdgeTests : TempFolderTest
{
    private string MainPath => Path.Combine(Folder, EncryptedPlanRepository.FileName);

    [Fact]
    public async Task A_save_that_cannot_put_the_plan_in_place_leaves_no_half_written_file()
    {
        Directory.CreateDirectory(MainPath); // something that is not a file stands where the plan goes

        var error = await Assert.ThrowsAnyAsync<Exception>(() => new EncryptedPlanRepository(Folder, Cipher).SaveAsync(TestData.Plan(), CancellationToken.None));

        // The OS decides which: an IOException on Linux and Android, an UnauthorizedAccessException on Windows from
        // .NET 11. PlanService handles both as a failed save.
        Assert.True(error is IOException or UnauthorizedAccessException, error.GetType().Name);
        Assert.Empty(Directory.GetFiles(Folder));
    }

    [Fact]
    public async Task A_keystore_shut_down_during_a_load_is_not_taken_for_a_damaged_plan()
    {
        var keystore = new InMemorySecretStore();
        var keys = new DataKeyProvider(keystore, () => false);
        var errors = new RecordingErrorReporter();
        var repository = new EncryptedPlanRepository(Folder, new AesGcmCipher(keys), errors);
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        keys.Dispose();
        var shutDown = new DataKeyProvider(keystore, () => true); // as after a restart: the key is not read yet
        shutDown.Dispose();
        var reopened = new EncryptedPlanRepository(Folder, new AesGcmCipher(shutDown), errors);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => reopened.LoadAsync(CancellationToken.None));

        Assert.Empty(errors.Reports);
        Assert.False(reopened.LastLoadUsedPreviousCopy);
    }

    public static TheoryData<Exception> Damage => new()
    {
        new CryptographicException("tag mismatch"),
        new InvalidDataException("not a plan"),
        new System.Text.Json.JsonException("not JSON"),
        new Maia.Domain.Common.DomainException("check.window", "out of range"),
        new ArgumentException("bad value"),
        new FormatException("bad date"),
        new InvalidOperationException("bad version"),
    };

    [Theory]
    [MemberData(nameof(Damage))]
    public async Task Every_kind_of_damage_to_the_main_file_opens_the_backup_copy_instead(Exception damage)
    {
        var key = await SaveTwiceAsync();
        var errors = new RecordingErrorReporter();
        var repository = new EncryptedPlanRepository(Folder, new AesGcmCipher(new FailsOnce(key, damage)), errors);

        var plan = await repository.LoadAsync(CancellationToken.None);

        Assert.NotNull(plan);
        Assert.True(repository.LastLoadUsedPreviousCopy);
        Assert.Equal((damage, EncryptedPlanRepository.FallbackOperation), Assert.Single(errors.Reports));
    }

    [Fact]
    public async Task A_main_file_that_cannot_be_read_from_disk_is_not_damage_and_never_falls_back()
    {
        var key = await SaveTwiceAsync();
        var failure = new IOException("disk");
        var repository = new EncryptedPlanRepository(Folder, new AesGcmCipher(new FailsOnce(key, failure)));

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => repository.LoadAsync(CancellationToken.None)));
        Assert.False(repository.LastLoadUsedPreviousCopy);
    }

    [Theory]
    [InlineData("\"7\"")]
    [InlineData("7.5")]
    public async Task A_plan_whose_version_is_not_a_whole_number_is_damaged(string version)
    {
        var plan = Encoding.UTF8.GetBytes($$"""{"schemaVersion":{{version}},"id":"6f1c2a6e-8a51-4e44-9d0f-6c1f8d7a2b10"}""");
        await File.WriteAllBytesAsync(MainPath, await Cipher.EncryptAsync(plan, "plan", CancellationToken.None));

        var error = await Assert.ThrowsAsync<PlanUnreadableException>(() => new EncryptedPlanRepository(Folder, Cipher).LoadAsync(CancellationToken.None));

        Assert.Equal(PlanUnreadableException.DamagedCode, error.Code);
    }

    [Theory]
    [InlineData("99")] // a number no category has
    [InlineData("Sometimes")] // a name no category has
    public async Task A_stored_category_maia_does_not_have_is_damaged(string category)
    {
        var repository = new EncryptedPlanRepository(Folder, Cipher);
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        var stored = JsonNode.Parse(await Cipher.DecryptAsync(await File.ReadAllBytesAsync(MainPath), "plan", CancellationToken.None))!;
        stored["checks"]![0]!["category"] = category;
        await File.WriteAllBytesAsync(MainPath, await Cipher.EncryptAsync(Encoding.UTF8.GetBytes(stored.ToJsonString()), "plan", CancellationToken.None));

        var error = await Assert.ThrowsAsync<PlanUnreadableException>(() => new EncryptedPlanRepository(Folder, Cipher).LoadAsync(CancellationToken.None));

        Assert.Equal(PlanUnreadableException.DamagedCode, error.Code);
        Assert.Contains($"'{category}'", error.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_result_file_save_stopped_part_way_leaves_nothing_behind()
    {
        var store = new EncryptedAttachmentStore(Folder, Cipher);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new byte[1024], cancelled.Token));

        Assert.Empty(Directory.GetFiles(Folder));
    }

    /// <summary>The data key <paramref name="key"/>, but the first time it is asked for, this failure.</summary>
    private sealed class FailsOnce(byte[] key, Exception failure) : IKeyProvider
    {
        private bool _failed;

        public ValueTask<byte[]> GetKeyAsync(CancellationToken cancellationToken)
        {
            if (!_failed)
            {
                _failed = true;
                throw failure;
            }

            return ValueTask.FromResult(key);
        }
    }

    /// <summary>A plan and its backup copy, saved with a key of their own.</summary>
    private async Task<byte[]> SaveTwiceAsync()
    {
        var key = AesGcmCipher.GenerateKey();
        var repository = new EncryptedPlanRepository(Folder, new AesGcmCipher(new InMemoryKeyProvider(key)));
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        return key;
    }
}

public sealed class DataKeyEdgeTests
{
    [Fact]
    public async Task Two_reads_at_once_get_the_one_key_read_once_from_the_keystore()
    {
        var keystore = new SlowSecretStore(Convert.ToBase64String(AesGcmCipher.GenerateKey()));
        using var provider = new DataKeyProvider(keystore, () => true);

        var first = provider.GetKeyAsync(CancellationToken.None).AsTask();
        var second = provider.GetKeyAsync(CancellationToken.None).AsTask();
        keystore.Answer();

        Assert.Same(await first, await second);
        Assert.Equal(1, keystore.Reads);
    }

    [Fact]
    public void A_missing_key_says_so_with_or_without_details()
    {
        var inner = new InvalidOperationException("keystore");

        Assert.Equal("The data key is not available.", new DataKeyUnavailableException().Message);
        Assert.Same(inner, new DataKeyUnavailableException("No key.", inner).InnerException);
    }

    /// <summary>A keystore that holds one key and answers the first read only when told to.</summary>
    private sealed class SlowSecretStore(string key) : ISecretStore
    {
        private readonly TaskCompletionSource _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Reads { get; private set; }

        public void Answer() => _answer.SetResult();

        public async Task<string?> GetAsync(string name)
        {
            Reads++;
            await _answer.Task;
            return key;
        }

        public Task SetAsync(string name, string value) => throw new InvalidOperationException("Never written in this test.");

        public void Remove(string name) => throw new InvalidOperationException("Never removed in this test.");
    }
}

public sealed class CipherEnvelopeTests
{
    private readonly AesGcmCipher _cipher = new(new InMemoryKeyProvider(AesGcmCipher.GenerateKey()));

    [Fact]
    public async Task A_file_of_another_encryption_format_version_is_refused()
    {
        var envelope = await _cipher.EncryptAsync("hello"u8.ToArray(), "plan", CancellationToken.None);
        envelope[4] = AesGcmCipher.FormatVersion + 1;

        var ex = await Assert.ThrowsAnyAsync<CryptographicException>(() => _cipher.DecryptAsync(envelope, "plan", CancellationToken.None));
        Assert.Contains("version 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Too_few_bytes_or_another_magic_have_no_version()
    {
        var envelope = await _cipher.EncryptAsync("hello"u8.ToArray(), "plan", CancellationToken.None);

        Assert.Equal(AesGcmCipher.FormatVersion, AesGcmCipher.PeekVersion(envelope));
        Assert.Null(AesGcmCipher.PeekVersion("MAIA"u8));
        Assert.Null(AesGcmCipher.PeekVersion("MAYA\u0001"u8));
    }
}

public sealed class SystemClockTests
{
    [Fact]
    public void Tells_the_time_and_zone_its_time_source_gives()
    {
        var athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");
        var instant = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero);

        var clock = new SystemClock(new FixedTime(instant, athens));

        Assert.Equal((instant, athens), (clock.UtcNow, clock.TimeZone));
    }

    [Fact]
    public void Without_a_time_source_it_tells_the_phone_s_time_and_zone()
    {
        var before = DateTimeOffset.UtcNow;

        var clock = new SystemClock();

        Assert.InRange(clock.UtcNow, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeZoneInfo.Local.Id, clock.TimeZone.Id);
    }

    private sealed class FixedTime(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
