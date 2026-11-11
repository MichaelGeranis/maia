namespace Maia.Domain.Common;

/// <summary>Identifies a pregnancy plan.</summary>
public readonly record struct PlanId(Guid Value)
{
    public static PlanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Identifies a check within a plan. Template checks keep their template key (for example
/// <c>ogtt</c>) so a plan can be matched against newer template versions; checks added by the
/// user get a <c>custom-</c> prefix.
/// </summary>
public readonly record struct CheckId
{
    private const string CustomPrefix = "custom-";

    public CheckId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64 || !value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new DomainException("check.id.invalid", "Not a valid check id.");
        }

        Value = value;
    }

    public string Value { get; }

    public bool IsCustom => Value.StartsWith(CustomPrefix, StringComparison.Ordinal);

    public static CheckId NewCustom() => new(CustomPrefix + Guid.NewGuid().ToString("N")[..12]);

    public override string ToString() => Value;
}

/// <summary>Identifies a result (a PDF report or a photo).</summary>
public readonly record struct AttachmentId(Guid Value)
{
    public static AttachmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

/// <summary>Identifies a phone contact linked to the care team.</summary>
public readonly record struct CareContactId(Guid Value)
{
    public static CareContactId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

/// <summary>Identifies a to-do task.</summary>
public readonly record struct TaskId(Guid Value)
{
    public static TaskId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

/// <summary>Identifies a question for the doctor.</summary>
public readonly record struct QuestionId(Guid Value)
{
    public static QuestionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

/// <summary>Identifies a blood pressure reading.</summary>
public readonly record struct ReadingId(Guid Value)
{
    public static ReadingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
