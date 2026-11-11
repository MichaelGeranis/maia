using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Domain.Plans;

namespace Maia.Infrastructure.Persistence;

/// <summary>
/// The encrypted backup file (ADR 0005). A key is derived from her backup password (PBKDF2-SHA256); the plaintext —
/// her result files, then the plan as <see cref="PlanDocument"/> JSON — is written as AES-256-GCM chunks whose
/// nonce and associated data carry the header, the chunk number and a "final" flag, so a reordered, swapped,
/// cut or extended file does not decrypt.
/// </summary>
public sealed class EncryptedBackupFormat : IPlanBackupFormat
{
    public const int DefaultIterations = 600_000;

    internal const int MinIterations = 100_000;
    internal const int MaxIterations = 10_000_000;
    internal const int ChunkSize = 64 * 1024;

    private const byte Version = 1;
    private const int SaltSize = 16;
    private const int NoncePrefixSize = 8;
    private const int TagSize = 16;
    private const int KeySize = 32;

    // Sanity limits, so a damaged length can't make the reader allocate gigabytes.
    private const long MaxFileBytes = 64L * 1024 * 1024;
    private const long MaxPlanBytes = 16L * 1024 * 1024;

    private const byte EndRecord = 0;
    private const byte FileRecord = 1;
    private const byte PlanRecord = 2;

    private static readonly byte[] Magic = "MAIABAK"u8.ToArray();
    private static readonly int HeaderSize = Magic.Length + 1 + 4 + SaltSize + NoncePrefixSize;

    private readonly int _iterations;

    public EncryptedBackupFormat()
        : this(DefaultIterations)
    {
    }

    /// <summary>Fewer key-derivation rounds, so tests stay quick. Never below <see cref="MinIterations"/>.</summary>
    internal EncryptedBackupFormat(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, MinIterations);
        _iterations = iterations;
    }

    public async Task<IReadOnlySet<string>> WriteAsync(Stream output, PregnancyPlan plan, Func<string, Task<byte[]?>> readFile, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = Version;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(Magic.Length + 1), _iterations);
        RandomNumberGenerator.Fill(header.AsSpan(Magic.Length + 5, SaltSize + NoncePrefixSize));

        var key = DeriveKey(password, header, _iterations);
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            using var chunks = new ChunkWriter(output, key, header);
            foreach (var blobId in plan.Results.Select(r => r.BlobId).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                if (await readFile(blobId).ConfigureAwait(false) is not { } content)
                {
                    skipped.Add(blobId);
                    continue;
                }

                await chunks.WriteRecordAsync(FileRecord, blobId, content, cancellationToken).ConfigureAwait(false);
            }

            // The reader stays strict (every blob id needs its file), so a result whose file is left out goes too.
            var document = PlanDocumentMapper.ToDocument(plan);
            document.Results.RemoveAll(r => r.BlobId is { } id && skipped.Contains(id));

            var json = JsonSerializer.SerializeToUtf8Bytes(document, PersistenceJsonContext.Default.PlanDocument);
            await chunks.WriteRecordAsync(PlanRecord, "plan", json, cancellationToken).ConfigureAwait(false);
            await chunks.WriteRecordAsync(EndRecord, string.Empty, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            await chunks.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return skipped;
    }

    public async Task<PregnancyPlan> ReadAsync(Stream input, string password, Func<ReadOnlyMemory<byte>, Task<string>> saveFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(saveFile);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var header = new byte[HeaderSize];
        var read = await input.ReadAtLeastAsync(header, HeaderSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (read < HeaderSize || !header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw BackupUnreadableException.NotABackup();
        }

        if (header[Magic.Length] != Version)
        {
            throw header[Magic.Length] > Version ? BackupUnreadableException.FromNewerApp() : BackupUnreadableException.Damaged();
        }

        var iterations = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(Magic.Length + 1));
        if (iterations is < MinIterations or > MaxIterations)
        {
            throw BackupUnreadableException.Damaged();
        }

        var key = DeriveKey(password, header, iterations);
        try
        {
            using var chunks = new ChunkReader(input, key, header);
            var newBlobIds = new Dictionary<string, string>(StringComparer.Ordinal);
            byte[]? planJson = null;
            while (true)
            {
                var kind = (await chunks.ReadAsync(1, cancellationToken).ConfigureAwait(false))[0];
                var nameLength = BinaryPrimitives.ReadUInt16BigEndian(await chunks.ReadAsync(2, cancellationToken).ConfigureAwait(false));
                var name = Encoding.UTF8.GetString(await chunks.ReadAsync(nameLength, cancellationToken).ConfigureAwait(false));
                var length = BinaryPrimitives.ReadInt64BigEndian(await chunks.ReadAsync(8, cancellationToken).ConfigureAwait(false));
                if (kind == EndRecord)
                {
                    break;
                }

                var limit = kind switch
                {
                    FileRecord => MaxFileBytes,
                    PlanRecord => MaxPlanBytes,
                    _ => throw BackupUnreadableException.Damaged(),
                };
                if (length < 0 || length > limit)
                {
                    throw BackupUnreadableException.Damaged();
                }

                var payload = await chunks.ReadAsync((int)length, cancellationToken).ConfigureAwait(false);
                if (kind == FileRecord)
                {
                    newBlobIds[name] = await saveFile(payload).ConfigureAwait(false);
                }
                else
                {
                    planJson = payload;
                }
            }

            await chunks.EnsureEndAsync(cancellationToken).ConfigureAwait(false);
            return ToPlan(planJson ?? throw BackupUnreadableException.Damaged(), newBlobIds);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static PregnancyPlan ToPlan(byte[] json, Dictionary<string, string> newBlobIds)
    {
        try
        {
            var node = JsonNode.Parse(json) as JsonObject ?? throw BackupUnreadableException.Damaged();
            if ((node["schemaVersion"]?.GetValue<int>() ?? 1) > PlanDocument.CurrentSchemaVersion)
            {
                throw BackupUnreadableException.FromNewerApp();
            }

            var migrated = PlanDocumentMigrator.Migrate(node);
            PointAtNewFiles(migrated, newBlobIds);
            var document = migrated.Deserialize(PersistenceJsonContext.Default.PlanDocument)!; // an object never reads as null
            return PlanDocumentMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException)
        {
            throw BackupUnreadableException.Damaged(ex);
        }
    }

    /// <summary>Replaces every <c>blobId</c> in the plan with the id the file got on this phone.</summary>
    private static void PointAtNewFiles(JsonNode? node, Dictionary<string, string> newBlobIds)
    {
        switch (node)
        {
            case JsonObject item:
                foreach (var (property, value) in item.ToList())
                {
                    if (property == "blobId" && value is JsonValue old && old.TryGetValue<string>(out var oldId))
                    {
                        item[property] = newBlobIds.TryGetValue(oldId, out var newId) ? newId : throw BackupUnreadableException.Damaged();
                    }
                    else
                    {
                        PointAtNewFiles(value, newBlobIds);
                    }
                }

                break;
            case JsonArray list:
                foreach (var value in list)
                {
                    PointAtNewFiles(value, newBlobIds);
                }

                break;
            default:
                break;
        }
    }

    private static byte[] DeriveKey(string password, byte[] header, int iterations)
    {
        // Normalised, so the same password typed on another keyboard (Greek accents) gives the same key.
        var secret = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(secret, header.AsSpan(Magic.Length + 5, SaltSize), iterations, HashAlgorithmName.SHA256, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>12-byte nonce: the file's random prefix, then the chunk number.</summary>
    private static byte[] NonceFor(byte[] header, uint counter)
    {
        var nonce = new byte[NoncePrefixSize + 4];
        header.AsSpan(Magic.Length + 5 + SaltSize, NoncePrefixSize).CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
        return nonce;
    }

    /// <summary>Associated data: the whole header, the chunk number and whether it is the last chunk.</summary>
    private static byte[] AssociatedData(byte[] header, uint counter, bool final)
    {
        var data = new byte[header.Length + 5];
        header.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(header.Length), counter);
        data[^1] = final ? (byte)1 : (byte)0;
        return data;
    }

    /// <summary>Buffers plaintext and writes it as chunks: <c>length (4) · final (1) · ciphertext · tag (16)</c>.</summary>
    private sealed class ChunkWriter : IDisposable
    {
        private readonly Stream _output;
        private readonly AesGcm _aes;
        private readonly byte[] _header;
        private readonly byte[] _buffer = new byte[ChunkSize];
        private int _filled;
        private uint _counter;

        public ChunkWriter(Stream output, byte[] key, byte[] header)
        {
            _output = output;
            _aes = new AesGcm(key, TagSize);
            _header = header;
        }

        public async Task WriteRecordAsync(byte kind, string name, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var head = new byte[1 + 2 + nameBytes.Length + 8];
            head[0] = kind;
            BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(1), checked((ushort)nameBytes.Length));
            nameBytes.CopyTo(head, 3);
            BinaryPrimitives.WriteInt64BigEndian(head.AsSpan(3 + nameBytes.Length), payload.Length);
            await WriteAsync(head, cancellationToken).ConfigureAwait(false);
            await WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        public Task CompleteAsync(CancellationToken cancellationToken) => FlushAsync(final: true, cancellationToken);

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(_buffer);
            _aes.Dispose();
        }

        private async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            while (!data.IsEmpty)
            {
                var take = Math.Min(data.Length, ChunkSize - _filled);
                data[..take].CopyTo(_buffer.AsMemory(_filled));
                _filled += take;
                data = data[take..];
                if (_filled == ChunkSize)
                {
                    await FlushAsync(final: false, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task FlushAsync(bool final, CancellationToken cancellationToken)
        {
            var frame = new byte[5 + _filled + TagSize];
            BinaryPrimitives.WriteInt32BigEndian(frame, _filled);
            frame[4] = final ? (byte)1 : (byte)0;
            _aes.Encrypt(
                NonceFor(_header, _counter),
                _buffer.AsSpan(0, _filled),
                frame.AsSpan(5, _filled),
                frame.AsSpan(5 + _filled, TagSize),
                AssociatedData(_header, _counter, final));
            await _output.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            _counter++;
            _filled = 0;
        }
    }

    /// <summary>Reads and checks chunks on demand. A failure on the first chunk means the password is wrong.</summary>
    private sealed class ChunkReader : IDisposable
    {
        private readonly Stream _input;
        private readonly AesGcm _aes;
        private readonly byte[] _header;
        private readonly byte[] _plain = new byte[ChunkSize];
        private int _position;
        private int _length;
        private uint _counter;
        private bool _final;

        public ChunkReader(Stream input, byte[] key, byte[] header)
        {
            _input = input;
            _aes = new AesGcm(key, TagSize);
            _header = header;
        }

        public async Task<byte[]> ReadAsync(int count, CancellationToken cancellationToken)
        {
            var result = new byte[count];
            var filled = 0;
            while (filled < count)
            {
                if (_position == _length)
                {
                    if (_final)
                    {
                        throw BackupUnreadableException.Damaged();
                    }

                    await NextChunkAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var take = Math.Min(count - filled, _length - _position);
                _plain.AsSpan(_position, take).CopyTo(result.AsSpan(filled));
                _position += take;
                filled += take;
            }

            return result;
        }

        /// <summary>After the end record: nothing may be left, neither plaintext nor more chunks.</summary>
        public async Task EnsureEndAsync(CancellationToken cancellationToken)
        {
            // The end record can fill a chunk exactly; the empty final chunk then follows.
            while (_position == _length && !_final)
            {
                await NextChunkAsync(cancellationToken).ConfigureAwait(false);
            }

            // The loop above leaves only at the final chunk or with plaintext still unread.
            if (_position != _length)
            {
                throw BackupUnreadableException.Damaged();
            }

            var extra = new byte[1];
            if (await _input.ReadAsync(extra, cancellationToken).ConfigureAwait(false) != 0)
            {
                throw BackupUnreadableException.Damaged();
            }
        }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(_plain);
            _aes.Dispose();
        }

        private async Task NextChunkAsync(CancellationToken cancellationToken)
        {
            var head = new byte[5];
            if (await _input.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false) < head.Length)
            {
                throw BackupUnreadableException.Damaged();
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(head);
            var final = head[4] == 1;
            if (length is < 0 or > ChunkSize || head[4] > 1)
            {
                throw BackupUnreadableException.Damaged();
            }

            var body = new byte[length + TagSize];
            if (await _input.ReadAtLeastAsync(body, body.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false) < body.Length)
            {
                throw BackupUnreadableException.Damaged();
            }

            try
            {
                _aes.Decrypt(NonceFor(_header, _counter), body.AsSpan(0, length), body.AsSpan(length, TagSize), _plain.AsSpan(0, length), AssociatedData(_header, _counter, final));
            }
            catch (AuthenticationTagMismatchException ex)
            {
                throw _counter == 0 ? BackupUnreadableException.WrongPassword(ex) : BackupUnreadableException.Damaged(ex);
            }

            _counter++;
            _position = 0;
            _length = length;
            _final = final;
        }
    }
}
