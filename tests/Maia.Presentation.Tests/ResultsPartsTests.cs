using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>The small rules behind Results: what a file is, how it is named, what Maia draws, and files shared in.</summary>
public sealed class ResultsPartsTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("application/pdf", false)]
    [InlineData("IMAGE/PNG", true)]
    public void A_file_is_a_photo_only_when_its_type_says_image(string? mediaType, bool expected) =>
        Assert.Equal(expected, PhotoPolicy.IsPhoto(mediaType!));

    [Theory]
    [InlineData(null, true, false)]
    [InlineData("text/plain", true, false)]
    [InlineData("image/heic", false, false)]
    [InlineData("image/heic", true, true)]
    [InlineData("application/pdf", false, true)]
    public void Maia_draws_pdfs_and_the_photos_this_phone_can_show(string? mediaType, bool drawsHeic, bool expected)
    {
        _app.Renderer.DrawsHeic = drawsHeic;

        Assert.Equal(expected, _app.Viewing.CanShow(mediaType));
    }

    [Fact]
    public void A_result_without_a_name_is_shared_as_result_with_its_ending() =>
        Assert.Equal("result.jpg", ShareFiles.FileName(null!, "image/jpeg"));

    [Fact]
    public void Nothing_to_share_asks_the_share_sheet_for_any_type() =>
        Assert.Equal("*/*", ShareFiles.CommonType([]));

    [Fact]
    public void A_report_kept_without_its_size_says_only_what_it_is()
    {
        var stored = ResultAttachment.Rehydrate(
            AttachmentId.New(), AttachmentKind.Document, "Old report.pdf", _app.Clock.UtcNow, "blob", "application/pdf", sizeBytes: null);

        var item = ResultItem.From(stored);

        Assert.Equal(("Old report", "PDF", "PDF"), (item.Name, item.Meta, item.KindLabel));
        Assert.True(item.IsReport);
        Assert.False(item.IsPhoto);
    }

    [Fact]
    public void Files_shared_in_tell_results_they_arrived_and_wait_to_be_kept()
    {
        var shares = _app.Shares;
        var heard = 0;
        shares.Changed += (_, _) => heard++;

        shares.Receive([new PickedFile("lab.pdf", "application/pdf", new byte[10])]);

        Assert.Equal(1, heard);
        Assert.Equal("lab.pdf", Assert.Single(shares.Pending).Name);
    }

    [Fact]
    public void Files_shared_in_with_no_screen_listening_still_wait_to_be_kept()
    {
        var shares = new ShareIntake(_app.Queries, _app.Navigator, _app.Notifier);

        shares.Receive([new PickedFile("lab.pdf", "application/pdf", new byte[10])]);

        Assert.Equal("lab.pdf", Assert.Single(shares.Take()).Name);
        Assert.Empty(shares.Pending);
    }
}
