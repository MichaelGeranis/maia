using System.Buffers.Binary;

namespace Maia.Infrastructure.Photos;

/// <summary>
/// Removes where a photo was taken from a JPEG, PNG or WebP kept as it is, without touching the picture:
/// <list type="bullet">
/// <item>Exif (a JPEG's APP1, a PNG's <c>eXIf</c>, a WebP's <c>EXIF</c>): the GPS entries and their values are
/// overwritten with zeros, leaving an empty GPS block that readers skip. The rest stays, orientation included.</item>
/// <item>XMP, which can hold a location too: a JPEG's XMP segments, a WebP's <c>XMP </c> chunk and a PNG's text chunks
/// (where XMP and copied Exif live) are left out.</item>
/// </list>
/// Anything it does not recognise is left as it is, so a damaged or unusual file is never made worse. Other types
/// (HEIC, which Maia turns into a JPEG where the phone can) come back unchanged.
/// </summary>
public static class PhotoLocation
{
    private const ushort GpsIfdTag = 0x8825;
    private const int EntrySize = 12;

    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = MakeCrcTable();

    /// <summary>The photo without its location; the same array when there was nothing to leave out.</summary>
    public static byte[] Remove(string mediaType, byte[] photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => Jpeg.Remove(photo),
            "image/png" => Png.Remove(photo),
            "image/webp" => WebP.Remove(photo),
            _ => photo,
        };
    }

    /// <summary>Zeros the GPS block of an Exif payload: a TIFF, with or without the <c>Exif\0\0</c> a JPEG puts first.</summary>
    private static void WipeGps(Span<byte> exif)
    {
        var tiff = exif.StartsWith("Exif\0\0"u8) ? exif[6..] : exif;
        if (tiff.Length < 8 || !(tiff.StartsWith("II"u8) || tiff.StartsWith("MM"u8)))
        {
            return;
        }

        var little = tiff[0] == (byte)'I';
        if (ReadUInt16(tiff, 2, little) != 42)
        {
            return;
        }

        var ifd0 = ReadUInt32(tiff, 4, little);
        if (FindValue(tiff, ifd0, GpsIfdTag, little) is { } gps)
        {
            WipeIfd(tiff, gps, little);
        }
    }

    /// <summary>The value (here an offset) stored in an IFD entry with this tag, if there is one.</summary>
    private static long? FindValue(Span<byte> tiff, long ifd, ushort tag, bool little)
    {
        if (ifd + 2 > tiff.Length)
        {
            return null;
        }

        var count = ReadUInt16(tiff, (int)ifd, little);
        for (var i = 0; i < count; i++)
        {
            var entry = ifd + 2 + ((long)i * EntrySize);
            if (entry + EntrySize > tiff.Length)
            {
                return null;
            }

            if (ReadUInt16(tiff, (int)entry, little) == tag)
            {
                return ReadUInt32(tiff, (int)entry + 8, little);
            }
        }

        return null;
    }

    /// <summary>Zeros every value of an IFD's entries, then the entries, and leaves it with none.</summary>
    private static void WipeIfd(Span<byte> tiff, long ifd, bool little)
    {
        if (ifd < 8 || ifd + 2 > tiff.Length)
        {
            return;
        }

        var count = ReadUInt16(tiff, (int)ifd, little);
        var entries = (int)Math.Min(count, (tiff.Length - ifd - 2) / EntrySize);
        for (var i = 0; i < entries; i++)
        {
            var entry = (int)ifd + 2 + (i * EntrySize);
            var size = TypeSize(ReadUInt16(tiff, entry + 2, little)) * (long)ReadUInt32(tiff, entry + 4, little);
            if (size > 4)
            {
                var offset = ReadUInt32(tiff, entry + 8, little);
                if (offset >= 8 && offset + size <= tiff.Length)
                {
                    tiff.Slice((int)offset, (int)size).Clear();
                }
            }
        }

        tiff.Slice((int)ifd + 2, entries * EntrySize).Clear();
        tiff.Slice((int)ifd, 2).Clear();
    }

    private static long TypeSize(ushort type) => type switch
    {
        1 or 2 or 6 or 7 => 1,
        3 or 8 => 2,
        4 or 9 or 11 => 4,
        5 or 10 or 12 => 8,
        _ => 0,
    };

    private static ushort ReadUInt16(Span<byte> data, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    /// <summary>Every caller has checked that the four bytes are there (a TIFF header or a whole IFD entry).</summary>
    private static long ReadUInt32(Span<byte> data, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);

    /// <summary>The photo with these byte ranges left out; the same array when there are none.</summary>
    private static byte[] Without(byte[] photo, List<Range> dropped)
    {
        if (dropped.Count == 0)
        {
            return photo;
        }

        using var output = new MemoryStream(photo.Length);
        var position = 0;
        foreach (var range in dropped)
        {
            var (start, length) = range.GetOffsetAndLength(photo.Length);
            output.Write(photo, position, start - position);
            position = start + length;
        }

        output.Write(photo, position, photo.Length - position);
        return output.ToArray();
    }

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>The CRC-32 a PNG chunk ends with, over its type and data.</summary>
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>JPEG: segments up to the picture; Exif in APP1, XMP in further APP1 segments.</summary>
    private static class Jpeg
    {
        private const byte Marker = 0xFF;
        private const byte StartOfImage = 0xD8;
        private const byte StartOfScan = 0xDA;
        private const byte EndOfImage = 0xD9;
        private const byte App1 = 0xE1;

        public static byte[] Remove(byte[] jpeg)
        {
            if (jpeg.Length < 4 || jpeg[0] != Marker || jpeg[1] != StartOfImage)
            {
                return jpeg;
            }

            var dropped = new List<Range>();
            var position = 2;
            while (position + 4 <= jpeg.Length && jpeg[position] == Marker)
            {
                var marker = jpeg[position + 1];
                if (marker == Marker)
                {
                    position++; // fill byte
                    continue;
                }

                if (marker is StartOfScan or EndOfImage)
                {
                    break; // the picture itself: metadata only comes before it
                }

                if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
                {
                    position += 2; // markers without a length
                    continue;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(position + 2));
                if (length < 2 || position + 2 + length > jpeg.Length)
                {
                    break;
                }

                if (marker == App1)
                {
                    var segment = jpeg.AsSpan(position + 4, length - 2);
                    if (segment.StartsWith("Exif\0\0"u8))
                    {
                        WipeGps(segment);
                    }
                    else if (segment.StartsWith("http://ns.adobe.com/xap/1.0/\0"u8) || segment.StartsWith("http://ns.adobe.com/xmp/extension/\0"u8))
                    {
                        dropped.Add(new Range(position, position + 2 + length));
                    }
                }

                position += 2 + length;
            }

            return Without(jpeg, dropped);
        }
    }

    /// <summary>PNG: chunks of length (4) · type (4) · data · CRC (4), up to IEND.</summary>
    private static class Png
    {
        public static byte[] Remove(byte[] png)
        {
            if (!png.AsSpan().StartsWith(PngSignature))
            {
                return png;
            }

            var dropped = new List<Range>();
            var position = PngSignature.Length;
            while (position + 12 <= png.Length)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
                if (length > int.MaxValue || position + 12 + (long)length > png.Length)
                {
                    break;
                }

                var type = png.AsSpan(position + 4, 4);
                var end = position + 12 + (int)length;
                if (type.SequenceEqual("eXIf"u8))
                {
                    WipeGps(png.AsSpan(position + 8, (int)length));
                    BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(end - 4), Crc(png.AsSpan(position + 4, 4 + (int)length)));
                }
                else if (type.SequenceEqual("tEXt"u8) || type.SequenceEqual("zTXt"u8) || type.SequenceEqual("iTXt"u8))
                {
                    dropped.Add(new Range(position, end));
                }
                else if (type.SequenceEqual("IEND"u8))
                {
                    break;
                }

                position = end;
            }

            return Without(png, dropped);
        }
    }

    /// <summary>WebP: a RIFF file of chunks, fourCC (4) · size (4, little-endian) · data, padded to an even size.</summary>
    private static class WebP
    {
        private const int HeaderSize = 12;
        private const byte XmpFlag = 0x04;

        public static byte[] Remove(byte[] webp)
        {
            if (webp.Length < HeaderSize || !webp.AsSpan().StartsWith("RIFF"u8) || !webp.AsSpan(8).StartsWith("WEBP"u8))
            {
                return webp;
            }

            var dropped = new List<Range>();
            var extended = -1;
            var position = HeaderSize;
            while (position + 8 <= webp.Length)
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(position + 4));
                var padded = size + (size & 1);
                if (position + 8 + padded > webp.Length)
                {
                    break;
                }

                var fourCc = webp.AsSpan(position, 4);
                var end = position + 8 + (int)padded;
                if (fourCc.SequenceEqual("VP8X"u8) && size > 0)
                {
                    extended = position + 8;
                }
                else if (fourCc.SequenceEqual("EXIF"u8))
                {
                    WipeGps(webp.AsSpan(position + 8, (int)size));
                }
                else if (fourCc.SequenceEqual("XMP "u8))
                {
                    dropped.Add(new Range(position, end));
                }

                position = end;
            }

            if (dropped.Count == 0)
            {
                return webp;
            }

            var cleaned = Without(webp, dropped);
            BinaryPrimitives.WriteUInt32LittleEndian(cleaned.AsSpan(4), (uint)(cleaned.Length - 8));
            if (extended >= 0)
            {
                cleaned[extended] &= unchecked((byte)~XmpFlag);
            }

            return cleaned;
        }
    }
}
