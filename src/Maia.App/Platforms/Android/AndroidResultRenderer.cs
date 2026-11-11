using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Systems;
using Maia.Presentation.Results;
using Stream = System.IO.Stream;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Draws results inside Maia: photos through the platform decoder, PDFs page by page with Android's PdfRenderer.
/// PdfRenderer reads from a file descriptor. From Android 11 that is an in-memory file, so the decrypted PDF never
/// touches the disk; before that it is a file in Maia's private cache whose name is removed the moment it is opened,
/// so it can't be found or opened again, and its content goes when the viewer closes.
/// </summary>
internal sealed class AndroidResultRenderer : IResultRenderer
{
    public static string Folder => System.IO.Path.Combine(FileSystem.CacheDirectory, "view");

    public bool CanShowImage(string mediaType) => mediaType?.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/png" or "image/webp" => true,
        "image/heic" or "image/heif" => OperatingSystem.IsAndroidVersionAtLeast(28),
        _ => false,
    };

    public Task<IPdfPages?> OpenPdfAsync(byte[] pdf) =>
        Task.Run(() =>
        {
            var descriptor = OperatingSystem.IsAndroidVersionAtLeast(30) ? InMemory(pdf) : OpenedAndUnlinked(pdf);
            try
            {
                return (IPdfPages?)new AndroidPdfPages(new PdfRenderer(descriptor), descriptor);
            }
            catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.SecurityException or Java.Lang.IllegalArgumentException)
            {
                // Damaged, or protected with a password: another app may still open it.
                descriptor.Close();
                return null;
            }
        });

    [System.Runtime.Versioning.SupportedOSPlatform("android30.0")]
    private static ParcelFileDescriptor InMemory(byte[] pdf)
    {
        var memory = Os.Memfd_create("result", 0) ?? throw new IOException("No in-memory file.");
        try
        {
            using (var output = new Java.IO.FileOutputStream(memory))
            {
                output.Write(pdf);
                output.Flush();
            }

            Os.Lseek(memory, 0, OsConstants.SeekSet);
            return ParcelFileDescriptor.Dup(memory) ?? throw new IOException("The in-memory file could not be opened.");
        }
        finally
        {
            Os.Close(memory);
        }
    }

    private static ParcelFileDescriptor OpenedAndUnlinked(byte[] pdf)
    {
        Directory.CreateDirectory(Folder);
        var path = System.IO.Path.Combine(Folder, Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, pdf);
            return ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly)
                ?? throw new IOException("The file could not be opened.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

/// <summary>An open PDF. PdfRenderer draws one page at a time, so drawing and closing take turns.</summary>
internal sealed class AndroidPdfPages : IPdfPages
{
    private const int JpegQuality = 92;

    private readonly PdfRenderer _renderer;
    private readonly ParcelFileDescriptor _descriptor;
    private readonly Lock _gate = new();
    private bool _closed;

    public AndroidPdfPages(PdfRenderer renderer, ParcelFileDescriptor descriptor)
    {
        _renderer = renderer;
        _descriptor = descriptor;
        var ratios = new List<double>(renderer.PageCount);
        for (var i = 0; i < renderer.PageCount; i++)
        {
            using var page = renderer.OpenPage(i);
            ratios.Add(page.Width > 0 ? page.Height / (double)page.Width : Math.Sqrt(2));
            page.Close();
        }

        Ratios = ratios;
    }

    public IReadOnlyList<double> Ratios { get; }

    public Task<byte[]> RenderAsync(int index, int width) =>
        Task.Run(() =>
        {
            Bitmap bitmap;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                using var page = _renderer.OpenPage(index);
                var height = Math.Max(1, (int)Math.Round(width * Ratios[index]));
                bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
                bitmap.EraseColor(global::Android.Graphics.Color.White);
                page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
                page.Close();
            }

            try
            {
                // A JPEG: much quicker to make and to show again than a lossless PNG of a full-width page, and a
                // fraction of its size in memory; at this quality small print stays as sharp on screen.
                using var output = new MemoryStream();
                bitmap.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, (Stream)output);
                return output.ToArray();
            }
            finally
            {
                bitmap.Recycle();
                bitmap.Dispose();
            }
        });

    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _renderer.Close();
            _renderer.Dispose();
            _descriptor.Close();
            _descriptor.Dispose();
        }
    }
}
