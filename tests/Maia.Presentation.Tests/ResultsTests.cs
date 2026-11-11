using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>How the files she adds on Results are read and kept.</summary>
public sealed class ResultsTests : IDisposable
{
    private static readonly PickedFile Report = new("Anomaly scan report.pdf", "application/pdf", new byte[410 * 1024]);
    private static readonly PickedFile Photo = new("20-week scan.jpg", "image/jpeg", new byte[(int)(2.3 * 1024 * 1024)]);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Uploading_keeps_the_files_in_her_results_each_saying_what_it_is_and_its_size()
    {
        var results = await OpenResultsAsync();
        _app.Files.Picks.Enqueue([Report, Photo]);

        await results.UploadCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { ("Anomaly scan report", "PDF · 410 KB"), ("20-week scan", "Photo · 2.3 MB") },
            _app.Repository.Stored!.Results.Select(r => ResultItem.From(r)).Select(r => (r.Name, r.Meta)));
        Assert.Equal("2 files added to your results.", _app.Notifier.Shown.Single());
        Assert.Equal(2, results.Results.Count);
    }

    [Fact]
    public async Task Files_maia_cannot_keep_are_explained_not_reported()
    {
        var results = await OpenResultsAsync();
        _app.Files.Picks.Enqueue([new PickedFile("notes.docx", "application/msword", new byte[10])]);

        await results.UploadCommand.ExecuteAsync(null);

        Assert.Equal("Maia keeps PDFs and photos. That file type can't be added.", results.Notice);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Several_files_are_kept_one_by_one_and_the_ones_maia_cannot_keep_are_named()
    {
        var results = await OpenResultsAsync();
        var opened = new List<string>();
        var cleaned = new List<string>();
        PickedFile Tracked(string name, string type, long? size, Func<Stream> open) =>
            new(name, type, size, () =>
            {
                opened.Add(name);
                return Task.FromResult(open());
            })
            { Cleanup = () => cleaned.Add(name) };
        _app.Files.Picks.Enqueue(
        [
            Tracked("notes.docx", "application/msword", 10, () => new MemoryStream(new byte[10])),
            Tracked("scan-150mb.pdf", "application/pdf", 150L * 1024 * 1024, () => throw new InvalidOperationException("Read before its size was checked.")),
            Tracked("Anomaly scan report.pdf", "application/pdf", null, () => new ForwardOnlyStream(410 * 1024)),
            Tracked("portal.pdf", "application/pdf", null, () => new ForwardOnlyStream(26L * 1024 * 1024)),
            Tracked("blank.pdf", "application/pdf", 0, () => new MemoryStream()),
            Tracked("offline.pdf", "application/pdf", null, () => throw new IOException("The cloud file is not on this phone.")),
        ]);

        await results.UploadCommand.ExecuteAsync(null);

        Assert.Equal("Anomaly scan report", Assert.Single(results.Results).Name);
        Assert.Equal("1 file added to your results.", _app.Notifier.Shown.Single());
        Assert.Equal(
            "These files couldn't be added: notes.docx (not a PDF or photo), scan-150mb.pdf (over 25 MB), portal.pdf (over 25 MB), "
            + "blank.pdf (empty), offline.pdf (couldn't be read).",
            results.Notice);
        Assert.DoesNotContain("notes.docx", opened);
        Assert.DoesNotContain("scan-150mb.pdf", opened);
        Assert.Equal(new[] { "notes.docx", "scan-150mb.pdf", "Anomaly scan report.pdf", "portal.pdf", "blank.pdf", "offline.pdf" }, cleaned);
        Assert.Equal("results.read", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_file_read_forward_stops_as_soon_as_it_passes_the_limit()
    {
        var stream = new ForwardOnlyStream(100L * 1024 * 1024);

        var content = await ResultFiling.ReadAsync(new PickedFile("big.pdf", "application/pdf", null, () => Task.FromResult<Stream>(stream)), 25L * 1024 * 1024);

        Assert.Null(content);
        Assert.True(stream.Position <= (25L * 1024 * 1024) + (80 * 1024));
    }

    [Fact]
    public async Task A_large_or_heic_photo_is_kept_as_a_smaller_jpeg_and_pdfs_are_never_changed()
    {
        await _app.CreatePlanAsync();
        _app.Photos.Sizes["IMG_2041.HEIC"] = new PhotoSize(3024, 4032);
        _app.Photos.Sizes["small.heic"] = new PhotoSize(1200, 900);

        await _app.Filing.KeepAsync(
            [
                new PickedFile("IMG_2041.HEIC", "image/heic", new byte[3 * 1024 * 1024]),
                new PickedFile("small.heic", "image/heic", new byte[200 * 1024]),
                Report,
            ]);

        var stored = _app.Repository.Stored!.Results;
        Assert.Equal(new[] { ("IMG_2041.jpg", "image/jpeg"), ("small.jpg", "image/jpeg"), (Report.Name, "application/pdf") }, stored.Select(r => (r.DisplayName, r.MediaType!)));
        Assert.Equal(new[] { ("IMG_2041.HEIC", 3000, 90), ("small.heic", 3000, 90) }, _app.Photos.Reencoded);
        Assert.Equal(300 * 1024, stored[0].SizeBytes);
        Assert.DoesNotContain(Report.Name, _app.Photos.Measured);
        Assert.Equal(410 * 1024, stored[2].SizeBytes);
    }

    [Fact]
    public async Task A_photo_already_small_enough_is_kept_as_it_is_without_its_location()
    {
        await _app.CreatePlanAsync();
        _app.Photos.Sizes[Photo.Name] = new PhotoSize(3000, 2250);

        await _app.Filing.KeepAsync([Photo]);

        var stored = Assert.Single(_app.Repository.Stored!.Results);
        Assert.Equal((Photo.Name, "image/jpeg", Photo.Size), (stored.DisplayName, stored.MediaType, stored.SizeBytes));
        Assert.Empty(_app.Photos.Reencoded);
        Assert.Equal(1, _app.Photos.LocationsWiped);
    }

    [Fact]
    public async Task A_small_png_or_webp_kept_as_it_is_loses_its_location_too_and_a_pdf_is_not_touched()
    {
        await _app.CreatePlanAsync();
        _app.Photos.Sizes["Screenshot.png"] = new PhotoSize(1080, 2400);
        _app.Photos.Sizes["scan.webp"] = new PhotoSize(800, 600);

        await _app.Filing.KeepAsync(
            [
                new PickedFile("Screenshot.png", "image/png", new byte[400 * 1024]),
                new PickedFile("scan.webp", "image/webp", new byte[90 * 1024]),
                Report,
            ]);

        Assert.Equal(3, _app.Repository.Stored!.Results.Count);
        Assert.Empty(_app.Photos.Reencoded);
        Assert.Equal(2, _app.Photos.LocationsWiped);
    }

    [Theory]
    [InlineData("image/jpeg", 4000, 3000, true)]
    [InlineData("image/jpeg", 3000, 2250, false)]
    [InlineData("image/png", 1080, 2400, false)]
    [InlineData("image/png", 1440, 3200, true)]
    [InlineData("image/heic", 1200, 900, true)]
    [InlineData("image/webp", 800, 600, false)]
    public void Photos_over_3000_pixels_or_in_heic_become_new_jpegs(string mediaType, int width, int height, bool expected) =>
        Assert.Equal(expected, PhotoPolicy.NeedsNewJpeg(mediaType, new PhotoSize(width, height)));

    [Theory]
    [InlineData("IMG_2041.HEIC", "IMG_2041.jpg")]
    [InlineData("scan.jpeg", "scan.jpeg")]
    [InlineData("Screenshot", "Screenshot.jpg")]
    public void A_photo_made_into_a_jpeg_is_named_as_one(string name, string expected) =>
        Assert.Equal(expected, PhotoPolicy.JpegName(name));

    [Theory]
    [InlineData(200, "1 KB")]
    [InlineData(410 * 1024, "410 KB")]
    [InlineData(2_411_724, "2.3 MB")]
    public void File_sizes_read_simply(long bytes, string expected) =>
        Assert.Equal(expected, ResultItem.FileSize(bytes));

    private async Task<ResultsViewModel> OpenResultsAsync()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        return vm;
    }
}
