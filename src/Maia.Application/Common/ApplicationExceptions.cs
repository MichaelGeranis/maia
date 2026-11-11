namespace Maia.Application.Common;

/// <summary>Base for expected, user-facing failures of a use case. <see cref="Code"/> maps to UI text.</summary>
public abstract class MaiaApplicationException : Exception
{
    protected MaiaApplicationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    protected MaiaApplicationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    protected MaiaApplicationException()
    {
        Code = "app.unknown";
    }

    protected MaiaApplicationException(string message)
        : base(message)
    {
        Code = "app.unknown";
    }

    protected MaiaApplicationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "app.unknown";
    }

    public string Code { get; }
}

public sealed class PlanNotFoundException : MaiaApplicationException
{
    public PlanNotFoundException()
        : base("plan.not_found", "No plan has been set up on this device.")
    {
    }

    public PlanNotFoundException(string message)
        : base("plan.not_found", message)
    {
    }

    public PlanNotFoundException(string message, Exception innerException)
        : base("plan.not_found", message, innerException)
    {
    }
}

public sealed class PlanAlreadyExistsException : MaiaApplicationException
{
    public PlanAlreadyExistsException()
        : base("plan.already_exists", "A plan already exists on this device.")
    {
    }

    public PlanAlreadyExistsException(string message)
        : base("plan.already_exists", message)
    {
    }

    public PlanAlreadyExistsException(string message, Exception innerException)
        : base("plan.already_exists", message, innerException)
    {
    }
}

/// <summary>
/// A plan is saved on this phone but Maia can't open it. <see cref="MaiaApplicationException.Code"/> says why. Her
/// files are left exactly as they are: only she decides, on the start-up screen, whether to replace them.
/// </summary>
public sealed class PlanUnreadableException : MaiaApplicationException
{
    /// <summary>The key that opens her files is gone from the keystore (for example after moving phones).</summary>
    public const string KeyUnavailableCode = "key.unavailable";

    /// <summary>The plan and its backup copy are both damaged.</summary>
    public const string DamagedCode = "plan.damaged";

    /// <summary>A newer version of Maia saved it. Updating Maia opens it; nothing may replace it until then.</summary>
    public const string NewerAppCode = "plan.newer_app";

    public PlanUnreadableException()
    {
    }

    public PlanUnreadableException(string message)
        : base(message)
    {
    }

    public PlanUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private PlanUnreadableException(string code, string message, Exception? innerException)
        : base(code, message, innerException ?? new InvalidDataException(message))
    {
    }

    /// <summary>Whether updating Maia will open it. Such a plan is never replaced, not even by a backup.</summary>
    public bool IsFromNewerApp => Code == NewerAppCode;

    public static PlanUnreadableException KeyUnavailable(Exception innerException) =>
        new(KeyUnavailableCode, "The key for the plan stored on this phone is not available.", innerException);

    public static PlanUnreadableException Damaged(Exception innerException) =>
        new(DamagedCode, "The plan stored on this phone and its backup copy are both unreadable.", innerException);

    public static PlanUnreadableException FromNewerApp(Exception? innerException = null) =>
        new(NewerAppCode, "The plan stored on this phone was saved by a newer version of Maia.", innerException);
}

/// <summary>A file the parent tried to attach was rejected (type or size).</summary>
public sealed class AttachmentRejectedException : MaiaApplicationException
{
    public AttachmentRejectedException()
    {
    }

    public AttachmentRejectedException(string message)
        : base(message)
    {
    }

    public AttachmentRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private AttachmentRejectedException(string code, string message)
        : base(code, message)
    {
    }

    public static AttachmentRejectedException UnsupportedType(string mediaType) =>
        new("attachment.type.unsupported", $"Files of type '{mediaType}' cannot be attached.");

    public static AttachmentRejectedException TooLarge(long maxBytes) =>
        new("attachment.size.too_large", $"Files larger than {maxBytes / (1024 * 1024)} MB cannot be attached.");

    public static AttachmentRejectedException Empty() =>
        new("attachment.size.empty", "The file is empty.");
}

/// <summary>A backup file could not be restored: wrong password, not a Maia backup, damaged, or from a newer app.</summary>
public sealed class BackupUnreadableException : MaiaApplicationException
{
    public const string WrongPasswordCode = "backup.password";
    public const string NotABackupCode = "backup.not_a_backup";
    public const string DamagedCode = "backup.damaged";
    public const string NewerAppCode = "backup.newer_app";

    public BackupUnreadableException()
    {
    }

    public BackupUnreadableException(string message)
        : base(message)
    {
    }

    public BackupUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private BackupUnreadableException(string code, string message, Exception? innerException)
        : base(code, message, innerException ?? new InvalidDataException(message))
    {
    }

    public static BackupUnreadableException WrongPassword(Exception innerException) =>
        new(WrongPasswordCode, "The password does not open this backup.", innerException);

    public static BackupUnreadableException NotABackup() =>
        new(NotABackupCode, "The file is not a Maia backup.", null);

    public static BackupUnreadableException Damaged(Exception? innerException = null) =>
        new(DamagedCode, "The backup is damaged or incomplete.", innerException);

    public static BackupUnreadableException FromNewerApp() =>
        new(NewerAppCode, "The backup was made by a newer version of Maia.", null);
}
