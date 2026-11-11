using Maia.Application.Common;

namespace Maia.Application.Tests;

/// <summary>Every failure she can be told about carries a stable code, however it was made.</summary>
public sealed class ApplicationExceptionTests
{
    private static readonly InvalidOperationException Cause = new("cause");

    [Fact]
    public void A_missing_plan_says_so_however_it_is_made()
    {
        Assert.Equal("plan.not_found", new PlanNotFoundException().Code);
        Assert.Equal(("plan.not_found", "gone"), Read(new PlanNotFoundException("gone")));
        var wrapped = new PlanNotFoundException("gone", Cause);
        Assert.Equal(("plan.not_found", "gone"), Read(wrapped));
        Assert.Same(Cause, wrapped.InnerException);
    }

    [Fact]
    public void A_plan_already_there_says_so_however_it_is_made()
    {
        Assert.Equal("plan.already_exists", new PlanAlreadyExistsException().Code);
        Assert.Equal(("plan.already_exists", "there"), Read(new PlanAlreadyExistsException("there")));
        var wrapped = new PlanAlreadyExistsException("there", Cause);
        Assert.Equal(("plan.already_exists", "there"), Read(wrapped));
        Assert.Same(Cause, wrapped.InnerException);
    }

    [Fact]
    public void Failures_made_without_a_reason_say_unknown_and_keep_their_message_and_cause()
    {
        MaiaApplicationException[] bare = [new PlanUnreadableException(), new AttachmentRejectedException(), new BackupUnreadableException()];
        MaiaApplicationException[] described = [new PlanUnreadableException("m"), new AttachmentRejectedException("m"), new BackupUnreadableException("m")];
        MaiaApplicationException[] wrapped = [new PlanUnreadableException("m", Cause), new AttachmentRejectedException("m", Cause), new BackupUnreadableException("m", Cause)];

        Assert.All(bare, e => Assert.Equal("app.unknown", e.Code));
        Assert.All(described, e => Assert.Equal(("app.unknown", "m"), Read(e)));
        Assert.All(wrapped, e =>
        {
            Assert.Equal(("app.unknown", "m"), Read(e));
            Assert.Same(Cause, e.InnerException);
        });
        Assert.False(new PlanUnreadableException().IsFromNewerApp);
    }

    [Fact]
    public void A_rejected_file_says_why()
    {
        Assert.Equal("attachment.type.unsupported", AttachmentRejectedException.UnsupportedType("text/plain").Code);
        Assert.Equal("attachment.size.empty", AttachmentRejectedException.Empty().Code);
        var tooLarge = AttachmentRejectedException.TooLarge(25 * 1024 * 1024);
        Assert.Equal("attachment.size.too_large", tooLarge.Code);
        Assert.Contains("25 MB", tooLarge.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_backup_or_plan_always_carries_a_cause()
    {
        Assert.IsType<InvalidDataException>(BackupUnreadableException.NotABackup().InnerException);
        Assert.IsType<InvalidDataException>(BackupUnreadableException.Damaged().InnerException);
        Assert.Same(Cause, BackupUnreadableException.Damaged(Cause).InnerException);
        Assert.IsType<InvalidDataException>(PlanUnreadableException.FromNewerApp().InnerException);
        Assert.True(PlanUnreadableException.FromNewerApp().IsFromNewerApp);
        Assert.False(PlanUnreadableException.Damaged(Cause).IsFromNewerApp);
    }

    private static (string Code, string Message) Read(MaiaApplicationException exception) => (exception.Code, exception.Message);
}
