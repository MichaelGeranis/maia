namespace Maia.Infrastructure;

/// <summary>
/// Writes a file and flushes it to the storage device before returning. Maia writes a temp file this way and only then
/// renames it into place, so a power cut or hard reboot right after a save can't leave the new file empty or torn.
/// </summary>
internal static class DurableFile
{
    public static async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);

            // fsync: the page cache alone would leave the data in memory for seconds after the rename.
            stream.Flush(flushToDisk: true);
        }
    }
}
