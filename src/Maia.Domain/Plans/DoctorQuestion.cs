using Maia.Domain.Common;

namespace Maia.Domain.Plans;

/// <summary>A question she wants to ask at her next appointment. Ticked off once it has been answered.</summary>
public sealed class DoctorQuestion
{
    public const int MaxTextLength = 300;

    internal DoctorQuestion(QuestionId id, string text, bool isAnswered)
        : this(id, text, isAnswered, checkLength: true)
    {
    }

    private DoctorQuestion(QuestionId id, string text, bool isAnswered, bool checkLength)
    {
        Validate(text, checkLength);
        Id = id;
        Text = text.Trim();
        IsAnswered = isAnswered;
    }

    public QuestionId Id { get; }

    public string Text { get; private set; }

    public bool IsAnswered { get; private set; }

    internal void SetAnswered(bool answered) => IsAnswered = answered;

    /// <summary>Rewords the question, under the same rules as a new one.</summary>
    /// <returns>Whether the text changed.</returns>
    internal bool ChangeText(string text)
    {
        Validate(text, checkLength: true);
        var trimmed = text.Trim();
        if (trimmed == Text)
        {
            return false;
        }

        Text = trimmed;
        return true;
    }

    /// <summary>
    /// Rebuilds a stored question. The length limit is for what she types now: a question saved under an older, longer
    /// limit still loads.
    /// </summary>
    internal static DoctorQuestion Restore(QuestionId id, string text, bool isAnswered) =>
        new(id, text, isAnswered, checkLength: false);

    private static void Validate(string text, bool checkLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (checkLength && text.Trim().Length > MaxTextLength)
        {
            throw new DomainException("question.text.too_long", "Question is too long.");
        }
    }
}
