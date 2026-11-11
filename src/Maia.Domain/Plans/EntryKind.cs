namespace Maia.Domain.Plans;

/// <summary>
/// What an entry she added to her journey is. Checks from her country's plan have none: their
/// <see cref="Templates.CheckCategory"/> says how they are recommended.
/// </summary>
public enum EntryKind
{
    /// <summary>A visit with a doctor, such as her OB-GYN.</summary>
    DoctorVisit = 0,

    /// <summary>An exam or a test, such as a scan or a blood test, usually one her doctor asked for.</summary>
    Exam = 1,

    /// <summary>A visit to a hospital.</summary>
    HospitalVisit = 2,

    /// <summary>Anything else she wants on her journey.</summary>
    Other = 3,
}
