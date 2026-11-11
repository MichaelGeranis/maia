using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// The phone's encrypted file store, as the test sets it up: it can turn a file away as it is saved, and a read can
/// take as long as the test chooses. Everything else is the in-memory store.
/// </summary>
internal sealed class ScriptedAttachmentStore : IAttachmentStore
{
    public InMemoryAttachmentStore Inner { get; } = new();

    /// <summary>Makes every save throw this.</summary>
    public Exception? RejectSaves { get; set; }

    /// <summary>When set, each read waits for it.</summary>
    public SlowStep? SlowRead { get; set; }

    public Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        RejectSaves is { } rejection ? Task.FromException<string>(rejection) : Inner.SaveAsync(content, cancellationToken);

    public async Task<byte[]> ReadAsync(string blobId, CancellationToken cancellationToken)
    {
        if (SlowRead is { } slow)
        {
            await slow.WaitAsync();
        }

        return await Inner.ReadAsync(blobId, cancellationToken);
    }

    public Task DeleteAsync(string blobId, CancellationToken cancellationToken) => Inner.DeleteAsync(blobId, cancellationToken);

    public Task DeleteAllAsync(CancellationToken cancellationToken) => Inner.DeleteAllAsync(cancellationToken);

    public Task<IReadOnlyList<StoredBlob>> ListAsync(CancellationToken cancellationToken) => Inner.ListAsync(cancellationToken);
}

/// <summary>A picker or camera that fails outright, as one can when the phone's file provider crashes.</summary>
internal sealed class FailingPicker(Exception failure) : IResultPicker
{
    public bool CanTakePhoto => true;

    public Task<IReadOnlyList<PickedFile>> PickAsync() => Task.FromException<IReadOnlyList<PickedFile>>(failure);

    public Task<PickedFile?> TakePhotoAsync() => Task.FromException<PickedFile?>(failure);
}

/// <summary>A phone with no app to open a file in: opening one outside Maia fails.</summary>
internal sealed class NoAppViewer : IResultViewer
{
    public Task OpenFileAsync(string name, string mediaType, byte[] content) =>
        Task.FromException(new InvalidOperationException("No app opens this file."));

    public Task ShareFilesAsync(IReadOnlyList<ResultToShare> results) => Task.CompletedTask;

    public void ClearCopies()
    {
    }
}

/// <summary>Photo work that fails: the phone can't decode the picture to make it small.</summary>
internal sealed class FailingPhotoProcessor : IPhotoProcessor
{
    public Task<PhotoSize?> MeasureAsync(PickedFile photo) => Task.FromResult<PhotoSize?>(null);

    public Task<byte[]> ToJpegAsync(PickedFile photo, int longEdge, int quality) =>
        Task.FromException<byte[]>(new NotSupportedException("The picture can't be decoded."));

    public byte[] WithoutLocation(string mediaType, byte[] photo) => photo;
}

/// <summary>A renderer that hands out <see cref="Pdf"/> for every PDF.</summary>
internal sealed class ScriptedRenderer(ScriptedPdf pdf) : IResultRenderer
{
    public ScriptedPdf Pdf => pdf;

    public bool CanShowImage(string mediaType) => true;

    public Task<IPdfPages?> OpenPdfAsync(byte[] content) => Task.FromResult<IPdfPages?>(pdf);
}

/// <summary>
/// An open PDF whose pages the test controls: some won't draw, and while <see cref="Hold"/> is set each page waits for
/// it, then fails if the PDF was closed meanwhile, as the phone's PDF renderer does.
/// </summary>
internal sealed class ScriptedPdf(int pages) : IPdfPages
{
    public IReadOnlyList<double> Ratios { get; } = Enumerable.Repeat(1.414, pages).ToList();

    /// <summary>Pages, by index, that fail to draw.</summary>
    public HashSet<int> Broken { get; } = [];

    public SlowStep? Hold { get; set; }

    public List<(int Index, int Width)> Rendered { get; } = [];

    public bool Disposed { get; private set; }

    public async Task<byte[]> RenderAsync(int index, int width)
    {
        Rendered.Add((index, width));
        if (Hold is { } hold)
        {
            await hold.WaitAsync();
        }

        ObjectDisposedException.ThrowIf(Disposed, this);
        return Broken.Contains(index) ? throw new IOException("The page is damaged.") : [(byte)(index + 1), (byte)(width % 256)];
    }

    public void Dispose() => Disposed = true;
}
