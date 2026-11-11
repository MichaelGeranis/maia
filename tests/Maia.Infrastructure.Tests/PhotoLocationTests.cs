using System.Buffers.Binary;
using System.Text;
using Maia.Infrastructure.Photos;

namespace Maia.Infrastructure.Tests;

public sealed class PhotoLocationTests
{
    /// <summary>Where the Exif data starts in a WebP made by <see cref="WebPWith"/>: after its header and the chunk's.</summary>
    private const int WebPExifData = 12 + 8;

    private static readonly byte[] Latitude = [37, 0, 0, 0, 1, 0, 0, 0, 58, 0, 0, 0, 1, 0, 0, 0, 0xD2, 0x04, 0, 0, 100, 0, 0, 0];
    private static readonly byte[] Picture = [0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x37, 0x58, 0x42, 0xFF, 0xD9];
    private static readonly byte[] Xmp = "<x:xmpmeta><exif:GPSLatitude>37,58.0N</exif:GPSLatitude></x:xmpmeta>"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Where_a_photo_was_taken_is_wiped_and_everything_else_kept(bool littleEndian)
    {
        var (jpeg, gpsIfd, latitude) = JpegWithLocation(littleEndian);
        var before = (byte[])jpeg.Clone();

        var cleaned = PhotoLocation.Remove("image/jpeg", jpeg);

        Assert.Same(jpeg, cleaned);
        Assert.All(cleaned.AsSpan(latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(cleaned.AsSpan(gpsIfd)));
        Assert.Contains("Pixel", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
        Assert.Equal(before.Length, cleaned.Length);
        Assert.Equal(Picture, cleaned[^Picture.Length..]);
    }

    [Fact]
    public void A_jpeg_loses_its_xmp_and_keeps_its_exif_and_picture()
    {
        var (jpeg, _, _) = JpegWithLocation(littleEndian: true);
        var xmp = Segment(0xE1, [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. Xmp]);
        byte[] withXmp = [.. jpeg[..^Picture.Length], .. xmp, .. Picture];

        var cleaned = PhotoLocation.Remove("image/jpeg", withXmp);

        Assert.Equal(jpeg.Length, cleaned.Length);
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
        Assert.Contains("Pixel", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
        Assert.Equal(Picture, cleaned[^Picture.Length..]);
    }

    [Fact]
    public void A_png_loses_its_location_and_its_text_and_stays_a_valid_png()
    {
        var (tiff, gps, latitude) = Tiff(littleEndian: false);
        var header = Chunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        var data = Chunk("IDAT", [0x78, 0x9C, 0x63, 0x60, 0x00, 0x00]);
        byte[] png =
        [
            .. PngSignature, .. header, .. Chunk("eXIf", tiff), .. Chunk("iTXt", [.. "XML:com.adobe.xmp\0\0\0\0\0"u8, .. Xmp]),
            .. Chunk("tEXt", "Comment\0Taken at the clinic"u8.ToArray()), .. Chunk("zTXt", "Comment\0\0x"u8.ToArray()), .. data, .. Chunk("IEND", []),
        ];

        var cleaned = PhotoLocation.Remove("image/png", png);

        var exifData = PngSignature.Length + header.Length + 8;
        Assert.All(cleaned.AsSpan(exifData + latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(cleaned.AsSpan(exifData + gps)));
        Assert.Equal(["IHDR", "eXIf", "IDAT", "IEND"], PngChunks(cleaned));
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
        Assert.DoesNotContain("clinic", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
    }

    [Fact]
    public void A_webp_loses_its_location_and_its_xmp_and_its_sizes_still_add_up()
    {
        var (tiff, gps, latitude) = Tiff(littleEndian: true);
        var extended = WebPChunk("VP8X", [0x0C, 0, 0, 0, 0, 0, 0, 0, 0, 0]); // EXIF and XMP flags
        var picture = WebPChunk("VP8L", [0x2F, 0, 0, 0, 0x10]);
        byte[] body = [.. "WEBP"u8, .. extended, .. picture, .. WebPChunk("EXIF", tiff), .. WebPChunk("XMP ", Xmp)];
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. body];
        BinaryPrimitives.WriteInt32LittleEndian(webp.AsSpan(4), body.Length);

        var cleaned = PhotoLocation.Remove("image/webp", webp);

        var exifData = 12 + extended.Length + picture.Length + 8;
        Assert.All(cleaned.AsSpan(exifData + latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(cleaned.AsSpan(exifData + gps)));
        Assert.Equal(cleaned.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(cleaned.AsSpan(4)));
        Assert.Equal(exifData + tiff.Length + (tiff.Length & 1), cleaned.Length);
        Assert.Equal(0x08, cleaned[12 + 8]);
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
    }

    [Fact]
    public void A_photo_without_location_or_a_file_of_another_type_is_left_alone()
    {
        byte[] plain = [0xFF, 0xD8, .. Picture];
        byte[] png = [.. PngSignature, .. Chunk("IDAT", [1, 2, 3]), .. Chunk("IEND", [])];
        var heic = "\0\0\0\x18ftypheic"u8.ToArray();

        Assert.Same(plain, PhotoLocation.Remove("image/jpeg", plain));
        Assert.Same(png, PhotoLocation.Remove("image/png", png));
        Assert.Same(heic, PhotoLocation.Remove("image/heic", heic));
        Assert.Same(png, PhotoLocation.Remove("image/jpeg", png));
        var wave = "RIFF\x04\0\0\0WAVE"u8.ToArray();
        Assert.Same(wave, PhotoLocation.Remove("image/webp", wave));
        Assert.Same(png, PhotoLocation.Remove("image/webp", png));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void A_damaged_file_does_not_throw(string mediaType)
    {
        var (tiff, _, _) = Tiff(littleEndian: true);
        var whole = mediaType switch
        {
            "image/jpeg" => JpegWithLocation(littleEndian: true).Jpeg,
            "image/png" => [.. PngSignature, .. Chunk("eXIf", tiff), .. Chunk("IEND", [])],
            _ => [.. "RIFF\0\0\0\0WEBP"u8, .. WebPChunk("EXIF", tiff), .. WebPChunk("XMP ", Xmp)],
        };

        for (var length = 0; length < whole.Length; length += 7)
        {
            _ = PhotoLocation.Remove(mediaType, whole[..length]);
        }
    }

    [Fact]
    public void The_media_type_is_read_whatever_its_case_and_a_photo_without_one_is_left_alone()
    {
        var (jpeg, _, latitude) = JpegWithLocation(littleEndian: true);
        var untouched = (byte[])jpeg.Clone();

        Assert.Same(untouched, PhotoLocation.Remove(null!, untouched));
        Assert.Equal(jpeg, untouched);

        var cleaned = PhotoLocation.Remove("IMAGE/JPEG", jpeg);
        Assert.All(cleaned.AsSpan(latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
    }

    public static TheoryData<byte[]> ExifItCannotRead => new()
    {
        new byte[] { (byte)'I', (byte)'I', 42, 0 }, // shorter than a TIFF header
        new byte[] { (byte)'X', (byte)'X', 42, 0, 8, 0, 0, 0, 0, 0 }, // neither byte order
        new byte[] { (byte)'I', (byte)'I', 43, 0, 8, 0, 0, 0, 0, 0 }, // not a TIFF
        new byte[] { (byte)'I', (byte)'I', 42, 0, 200, 0, 0, 0, 0, 0 }, // its first block past the end
        new byte[] { (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 1, 0, 0x25, 0x88, 4, 0 }, // its first block cut short
    };

    [Theory]
    [MemberData(nameof(ExifItCannotRead))]
    public void Exif_it_cannot_read_is_left_exactly_as_it_is(byte[] tiff)
    {
        byte[] jpeg = [0xFF, 0xD8, .. Segment(0xE1, [.. "Exif\0\0"u8, .. tiff]), .. Picture];
        var before = (byte[])jpeg.Clone();

        var cleaned = PhotoLocation.Remove("image/jpeg", jpeg);

        Assert.Same(jpeg, cleaned);
        Assert.Equal(before, cleaned);
    }

    [Fact]
    public void Exif_without_a_gps_block_is_left_exactly_as_it_is()
    {
        var (tiff, _, _) = Tiff(littleEndian: true);
        Write16(tiff, 8 + 14, 0x8769, little: true); // the pointer to the camera's Exif block, not to GPS
        byte[] png = [.. PngSignature, .. Chunk("eXIf", tiff), .. Chunk("IEND", [])];
        var before = (byte[])png.Clone();

        Assert.Equal(before, PhotoLocation.Remove("image/png", png));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5000)]
    public void A_gps_pointer_outside_the_exif_wipes_nothing(int gpsPointer)
    {
        var (tiff, gps, latitude) = Tiff(littleEndian: false);
        Write32(tiff, 8 + 14 + 8, gpsPointer, little: false);
        byte[] png = [.. PngSignature, .. Chunk("eXIf", tiff), .. Chunk("IEND", [])];
        const int exifData = 8 + 8;

        var cleaned = PhotoLocation.Remove("image/png", png);

        Assert.Equal(Latitude, cleaned.AsSpan(exifData + latitude, Latitude.Length).ToArray());
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(cleaned.AsSpan(exifData + gps)));
    }

    [Theory]
    [InlineData(1, 24, true)] // byte
    [InlineData(2, 24, true)] // text
    [InlineData(6, 24, true)] // signed byte
    [InlineData(7, 24, true)] // undefined
    [InlineData(3, 12, true)] // short
    [InlineData(8, 12, true)] // signed short
    [InlineData(4, 6, true)] // long
    [InlineData(9, 6, true)] // signed long
    [InlineData(11, 6, true)] // float
    [InlineData(5, 3, true)] // rational
    [InlineData(10, 3, true)] // signed rational
    [InlineData(12, 3, true)] // double
    [InlineData(0, 24, false)] // no such type: no size, so no value to find
    [InlineData(13, 24, false)]
    public void Every_kind_of_gps_value_kept_outside_its_entry_is_wiped(int type, int count, bool wiped)
    {
        var (tiff, gps, values) = GpsTiff((ushort)type, count, valueAt: null);

        var cleaned = PhotoLocation.Remove("image/webp", WebPWith(tiff));

        var expected = wiped ? new byte[24] : Enumerable.Repeat((byte)0xAB, 24).ToArray();
        Assert.Equal(expected, cleaned.AsSpan(WebPExifData + values, 24).ToArray());
        Assert.Equal(new byte[2 + 12], cleaned.AsSpan(WebPExifData + gps, 2 + 12).ToArray());
    }

    [Theory]
    [InlineData(4)] // inside the TIFF header
    [InlineData(70)] // runs past the end
    public void A_gps_value_said_to_be_outside_the_exif_is_not_touched(int valueAt)
    {
        var (tiff, gps, values) = GpsTiff(1, 24, valueAt);
        var header = tiff[..8];

        var cleaned = PhotoLocation.Remove("image/webp", WebPWith(tiff));

        Assert.Equal(header, cleaned.AsSpan(WebPExifData, 8).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)0xAB, 24).ToArray(), cleaned.AsSpan(WebPExifData + values, 24).ToArray());
        Assert.Equal(new byte[2 + 12], cleaned.AsSpan(WebPExifData + gps, 2 + 12).ToArray());
    }

    [Fact]
    public void A_jpeg_s_other_segments_markers_and_fill_bytes_are_stepped_over_to_reach_its_exif()
    {
        var (jpeg, gpsIfd, latitude) = JpegWithLocation(littleEndian: true);
        var extension = Segment(0xE1, [.. "http://ns.adobe.com/xmp/extension/\0"u8, .. Xmp]);
        byte[] kept =
        [
            0xFF, 0xD8,
            .. Segment(0xE0, "JFIF\0\x01\x02"u8.ToArray()), // APP0
            0xFF, 0xFF, // a fill byte
            0xFF, 0x01, // markers that have no length
            0xFF, 0xD0,
            0xFF, 0xD4,
            0xFF, 0xD7,
            .. Segment(0xDB, [0, 1, 2, 3]), // a quantisation table
            .. Segment(0xC0, [8, 0, 1, 0, 1, 1]), // the frame header
            .. Segment(0xE1, "Other\0metadata"u8.ToArray()), // APP1, but neither Exif nor XMP
        ];
        byte[] photo = [.. kept, .. extension, .. jpeg[2..]];

        var cleaned = PhotoLocation.Remove("image/jpeg", photo);

        var shift = kept.Length - 2;
        Assert.Equal(photo.Length - extension.Length, cleaned.Length);
        Assert.Equal(kept, cleaned[..kept.Length]);
        Assert.All(cleaned.AsSpan(shift + latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(cleaned.AsSpan(shift + gpsIfd)));
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(cleaned), StringComparison.Ordinal);
    }

    [Fact]
    public void A_jpeg_segment_with_an_impossible_length_ends_the_search()
    {
        var (jpeg, _, _) = JpegWithLocation(littleEndian: true);
        byte[] photo = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01, .. jpeg[2..]];
        var before = (byte[])photo.Clone();

        var cleaned = PhotoLocation.Remove("image/jpeg", photo);

        Assert.Same(photo, cleaned);
        Assert.Equal(before, cleaned);
    }

    [Fact]
    public void A_png_chunk_longer_than_any_file_ends_the_search()
    {
        byte[] huge = [0xFF, 0xFF, 0xFF, 0xFF, .. "tEXt"u8, .. "Comment\0Taken at the clinic"u8, 0, 0, 0, 0];
        byte[] png = [.. PngSignature, .. huge, .. Chunk("tEXt", "Comment\0Taken at the clinic"u8.ToArray()), .. Chunk("IEND", [])];
        var before = (byte[])png.Clone();

        var cleaned = PhotoLocation.Remove("image/png", png);

        Assert.Same(png, cleaned);
        Assert.Equal(before, cleaned);
    }

    [Fact]
    public void A_webp_without_extended_flags_loses_its_xmp_and_an_empty_extended_header_is_left_alone()
    {
        var emptyExtended = WebPChunk("VP8X", []);
        var picture = WebPChunk("VP8L", [0x2F, 0, 0, 0, 0x10]);
        byte[] body = [.. "WEBP"u8, .. emptyExtended, .. picture, .. WebPChunk("XMP ", Xmp)];
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. body];
        BinaryPrimitives.WriteInt32LittleEndian(webp.AsSpan(4), body.Length);

        var cleaned = PhotoLocation.Remove("image/webp", webp);

        byte[] expected = [.. "WEBP"u8, .. emptyExtended, .. picture];
        Assert.Equal(expected, cleaned[8..]);
        Assert.Equal(cleaned.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(cleaned.AsSpan(4)));
    }

    [Fact]
    public void A_webp_with_nothing_to_leave_out_is_cleaned_in_place()
    {
        var (tiff, _, latitude) = Tiff(littleEndian: true);

        var webp = WebPWith(tiff);
        var cleaned = PhotoLocation.Remove("image/webp", webp);

        Assert.Same(webp, cleaned);
        Assert.All(cleaned.AsSpan(WebPExifData + latitude, Latitude.Length).ToArray(), b => Assert.Equal(0, b));
    }

    /// <summary>A WebP holding only this Exif.</summary>
    private static byte[] WebPWith(byte[] tiff)
    {
        byte[] body = [.. "WEBP"u8, .. WebPChunk("EXIF", tiff)];
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. body];
        BinaryPrimitives.WriteInt32LittleEndian(webp.AsSpan(4), body.Length);
        return webp;
    }

    /// <summary>
    /// A TIFF whose GPS block has one entry of this type and count, its value kept at <paramref name="valueAt"/> (by
    /// default the 24 bytes of 0xAB at the end).
    /// </summary>
    private static (byte[] Tiff, int GpsIfd, int Values) GpsTiff(ushort type, int count, int? valueAt)
    {
        // TIFF: header (8), IFD0 with the GPS pointer (2 + 12 + 4), GPS IFD (2 + 12 + 4), then the values (24).
        const int ifd0 = 8;
        const int gps = ifd0 + 2 + 12 + 4;
        const int values = gps + 2 + 12 + 4;
        var tiff = new byte[values + 24];
        tiff[0] = tiff[1] = (byte)'I';
        Write16(tiff, 2, 42, little: true);
        Write32(tiff, 4, ifd0, little: true);
        Write16(tiff, ifd0, 1, little: true);
        Entry(tiff, ifd0 + 2, 0x8825, 4, 1, gps, little: true);
        Write16(tiff, gps, 1, little: true);
        Entry(tiff, gps + 2, 0x0004, type, count, valueAt ?? values, little: true);
        tiff.AsSpan(values).Fill(0xAB);
        return (tiff, gps, values);
    }

    /// <summary>A TIFF that names the camera and holds a GPS latitude, as a phone camera writes Exif.</summary>
    private static (byte[] Tiff, int GpsIfd, int Latitude) Tiff(bool littleEndian)
    {
        // TIFF: header (8), IFD0 with Make and the GPS pointer (2 + 24 + 4), "Pixel\0", GPS IFD (2 + 24 + 4), latitude.
        const int ifd0 = 8;
        const int make = ifd0 + 2 + 24 + 4;
        const int gps = make + 6;
        const int latitude = gps + 2 + 24 + 4;
        var tiff = new byte[latitude + Latitude.Length];
        tiff[0] = tiff[1] = littleEndian ? (byte)'I' : (byte)'M';
        Write16(tiff, 2, 42, littleEndian);
        Write32(tiff, 4, ifd0, littleEndian);

        Write16(tiff, ifd0, 2, littleEndian);
        Entry(tiff, ifd0 + 2, 0x010F, 2, 6, make, littleEndian);
        Entry(tiff, ifd0 + 14, 0x8825, 4, 1, gps, littleEndian);
        "Pixel\0"u8.CopyTo(tiff.AsSpan(make));

        Write16(tiff, gps, 2, littleEndian);
        Entry(tiff, gps + 2, 0x0001, 2, 2, 0, littleEndian);
        tiff[gps + 2 + 8] = (byte)'N';
        Entry(tiff, gps + 14, 0x0002, 5, 3, latitude, littleEndian);
        Latitude.CopyTo(tiff, latitude);
        return (tiff, gps, latitude);
    }

    /// <summary>A tiny JPEG whose Exif names the camera and holds a GPS latitude.</summary>
    private static (byte[] Jpeg, int GpsIfd, int Latitude) JpegWithLocation(bool littleEndian)
    {
        var (tiff, gps, latitude) = Tiff(littleEndian);
        var jpeg = new byte[] { 0xFF, 0xD8 }.Concat(Segment(0xE1, [.. "Exif\0\0"u8, .. tiff])).Concat(Picture).ToArray();
        const int tiffStart = 2 + 4 + 6;
        return (jpeg, tiffStart + gps, tiffStart + latitude);
    }

    private static byte[] Segment(byte marker, byte[] payload)
    {
        byte[] segment = [0xFF, marker, 0, 0, .. payload];
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        return segment;
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        byte[] chunk = [0, 0, 0, 0, .. Encoding.ASCII.GetBytes(type), .. data, 0, 0, 0, 0];
        BinaryPrimitives.WriteInt32BigEndian(chunk, data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunk.Length - 4), Crc(chunk.AsSpan(4, 4 + data.Length)));
        return chunk;
    }

    /// <summary>The chunk types in a PNG, each checked against its CRC.</summary>
    private static List<string> PngChunks(byte[] png)
    {
        var types = new List<string>();
        var position = PngSignature.Length;
        while (position < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            Assert.Equal(Crc(png.AsSpan(position + 4, 4 + length)), BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length)));
            types.Add(Encoding.ASCII.GetString(png, position + 4, 4));
            position += 12 + length;
        }

        return types;
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            c ^= b;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
        }

        return c ^ 0xFFFFFFFFu;
    }

    private static byte[] WebPChunk(string fourCc, byte[] data)
    {
        byte[] chunk = [.. Encoding.ASCII.GetBytes(fourCc), 0, 0, 0, 0, .. data, .. (data.Length % 2 == 1 ? new byte[] { 0 } : [])];
        BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(4), data.Length);
        return chunk;
    }

    private static void Entry(byte[] tiff, int at, ushort tag, ushort type, int count, int value, bool little)
    {
        Write16(tiff, at, tag, little);
        Write16(tiff, at + 2, type, little);
        Write32(tiff, at + 4, count, little);
        if (value != 0)
        {
            Write32(tiff, at + 8, value, little);
        }
    }

    private static void Write16(byte[] data, int at, ushort value, bool little)
    {
        if (little)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value);
        }
    }

    private static void Write32(byte[] data, int at, int value, bool little)
    {
        if (little)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
        }
        else
        {
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(at), value);
        }
    }
}
