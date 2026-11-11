using Android.Graphics;
using Android.Media;
using Maia.Infrastructure.Photos;
using Maia.Presentation.Results;
using Stream = System.IO.Stream;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Photos on Android (ADR 0002): sizes are read from the header, turned by the Exif orientation; a new JPEG is
/// decoded at the smallest power-of-two step still at least as large as needed, scaled and turned upright in one go,
/// and written without any metadata. HEIC needs Android 9; before that the photo can't be read here and is kept as
/// it is.
/// </summary>
internal sealed class AndroidPhotoProcessor : IPhotoProcessor
{
    public Task<PhotoSize?> MeasureAsync(PickedFile photo) =>
        Task.Run(async () =>
        {
            var (width, height) = await BoundsAsync(photo);
            if (width <= 0 || height <= 0)
            {
                return (PhotoSize?)null;
            }

            return Turns(await OrientationAsync(photo)) ? new PhotoSize(height, width) : new PhotoSize(width, height);
        });

    public Task<byte[]> ToJpegAsync(PickedFile photo, int longEdge, int quality) =>
        Task.Run(async () =>
        {
            var (width, height) = await BoundsAsync(photo);
            var orientation = await OrientationAsync(photo);
            var sample = 1;
            while (Math.Max(width, height) / (sample * 2) >= longEdge)
            {
                sample *= 2;
            }

            try
            {
                return await EncodeAsync(photo, sample, longEdge, quality, orientation);
            }
            catch (Java.Lang.OutOfMemoryError)
            {
                // A phone short of memory: one step smaller is still sharp enough to read a report.
                return await EncodeAsync(photo, sample * 2, longEdge, quality, orientation);
            }
        });

    public byte[] WithoutLocation(string mediaType, byte[] photo) => PhotoLocation.Remove(mediaType, photo);

    private static async Task<byte[]> EncodeAsync(PickedFile photo, int sample, int longEdge, int quality, int orientation)
    {
        Bitmap decoded;
        await using (var stream = await photo.OpenAsync())
        {
            decoded = BitmapFactory.DecodeStream(stream, null, new BitmapFactory.Options { InSampleSize = sample })
                ?? throw new IOException("The photo could not be decoded.");
        }

        Bitmap? upright = null;
        try
        {
            var scale = Math.Min(1f, longEdge / (float)Math.Max(decoded.Width, decoded.Height));
            using var matrix = new Matrix();
            matrix.PostScale(scale, scale);
            Orient(matrix, orientation);
            upright = Bitmap.CreateBitmap(decoded, 0, 0, decoded.Width, decoded.Height, matrix, filter: true);
            using var output = new MemoryStream();
            if (!upright.Compress(Bitmap.CompressFormat.Jpeg!, quality, output))
            {
                throw new IOException("The photo could not be written as a JPEG.");
            }

            return output.ToArray();
        }
        finally
        {
            if (upright is not null && !ReferenceEquals(upright, decoded))
            {
                upright.Recycle();
                upright.Dispose();
            }

            decoded.Recycle();
            decoded.Dispose();
        }
    }

    private static async Task<(int Width, int Height)> BoundsAsync(PickedFile photo)
    {
        var options = new BitmapFactory.Options { InJustDecodeBounds = true };
        await using (var stream = await photo.OpenAsync())
        {
            BitmapFactory.DecodeStream(stream, null, options);
        }

        return (options.OutWidth, options.OutHeight);
    }

    /// <summary>The Exif orientation (1 = upright); 1 when there is none or it can't be read.</summary>
    private static async Task<int> OrientationAsync(PickedFile photo)
    {
        try
        {
            await using var stream = await photo.OpenAsync();
            return ReadOrientation(stream);
        }
        catch (Java.Lang.Exception)
        {
            return 1;
        }
    }

    private static int ReadOrientation(Stream stream)
    {
        using var exif = new ExifInterface(stream);
        return exif.GetAttributeInt(ExifInterface.TagOrientation, 1);
    }

    /// <summary>Orientations 5–8 are turned a quarter, so width and height swap.</summary>
    private static bool Turns(int orientation) => orientation is >= 5 and <= 8;

    private static void Orient(Matrix matrix, int orientation)
    {
        switch (orientation)
        {
            case 2:
                matrix.PostScale(-1, 1);
                break;
            case 3:
                matrix.PostRotate(180);
                break;
            case 4:
                matrix.PostScale(1, -1);
                break;
            case 5:
                matrix.PostRotate(90);
                matrix.PostScale(-1, 1);
                break;
            case 6:
                matrix.PostRotate(90);
                break;
            case 7:
                matrix.PostRotate(270);
                matrix.PostScale(-1, 1);
                break;
            case 8:
                matrix.PostRotate(270);
                break;
            default:
                break;
        }
    }
}
