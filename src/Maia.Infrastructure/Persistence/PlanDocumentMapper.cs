using System.Globalization;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Infrastructure.Persistence;

/// <summary>Maps between the domain aggregate and its stored document.</summary>
internal static class PlanDocumentMapper
{
    /// <summary>What a check switched off at setup was stored as, before such checks were no longer kept: it is left out.</summary>
    private const string RemovedState = "NotNeeded";

    /// <summary>What "not booked" was stored as before it had that name.</summary>
    private const string OldNotBookedState = "Pending";

    public static PlanDocument ToDocument(PregnancyPlan plan) => new()
    {
        Id = plan.Id.Value,
        Revision = plan.Revision,
        DueDate = plan.Dating.DueDate,
        TemplateId = plan.Template.Id,
        TemplateVersion = plan.Template.Version,
        Language = plan.Language,
        CreatedAt = plan.CreatedAt,
        Checks = plan.Checks.Select(ToDocument).ToList(),
        Tasks = plan.Tasks.Select(t => new TaskDocument
        {
            Id = t.Id.Value,
            Title = t.Title,
            DueDate = t.DueDate,
            DueTime = t.DueTime,
            Trimester = t.Trimester?.ToString(),
            Week = t.Week,
            IsDone = t.IsDone,
        }).ToList(),
        Contacts = plan.CareTeam.Contacts.Select(c => new ContactDocument
        {
            Id = c.Id.Value,
            Role = c.Role.ToString(),
            DisplayName = c.DisplayName,
            PhoneNumber = c.PhoneNumber,
            Label = c.Label,
            DeviceContactId = c.DeviceContactId,
        }).ToList(),
        Questions = plan.Questions.Select(q => new QuestionDocument { Id = q.Id.Value, Text = q.Text, IsAnswered = q.IsAnswered }).ToList(),
        TimeZone = plan.TimeZoneId,
        Results = plan.Results.Select(ToDocument).ToList(),
        BloodPressure = plan.BloodPressure.Select(r => new ReadingDocument
        {
            Id = r.Id.Value,
            Systolic = r.Systolic,
            Diastolic = r.Diastolic,
            Date = r.Date,
            Time = r.Time,
        }).ToList(),
    };

    /// <param name="document">The stored plan.</param>
    /// <param name="repairs">Gets a code for each stored record the domain had to repair (see <see cref="PregnancyPlan.Rehydrate"/>).</param>
    public static PregnancyPlan ToDomain(PlanDocument document, ICollection<string>? repairs = null)
    {
        var contacts = document.Contacts.Select(c => new CareContact(
            new CareContactId(c.Id),
            Parse<CareRole>(c.Role),
            c.DisplayName,
            c.PhoneNumber,
            c.Label,
            c.DeviceContactId));

        return PregnancyPlan.Rehydrate(
            new PlanId(document.Id),
            PregnancyDating.FromDueDate(document.DueDate),
            new TemplateReference(document.TemplateId, document.TemplateVersion),
            document.Language,
            document.CreatedAt,
            document.Checks.Where(c => c.State != RemovedState).Select(ToDomain),
            document.Tasks.Select(t => PregnancyPlan.RehydrateTask(
                new TaskId(t.Id),
                t.Title,
                t.DueDate,
                t.DueTime,
                t.IsDone,
                repairs,
                t.Trimester is null ? null : Parse<Trimester>(t.Trimester),
                t.Week)),
            new CareTeam(contacts),
            document.Revision,
            document.Questions.Select(q => PregnancyPlan.RehydrateQuestion(new QuestionId(q.Id), q.Text, q.IsAnswered)),
            document.Results.Select(ToDomain),
            repairs,
            document.TimeZone,
            document.BloodPressure.Select(r => PregnancyPlan.RehydrateReading(new ReadingId(r.Id), r.Systolic, r.Diastolic, r.Date, r.Time)));
    }

    private static AppointmentDocument? ToDocument(Appointment? appointment) => appointment is { } a
        ? new AppointmentDocument { Date = a.Date, Time = a.Time, Location = a.Location, ContactId = a.With?.Value }
        : null;

    private static Appointment? ToDomain(AppointmentDocument? document) => document is { } a
        ? new Appointment(a.Date, a.Time, a.Location, a.ContactId is { } id ? new CareContactId(id) : null)
        : null;

    private static CheckDocument ToDocument(PlannedCheck check) => new()
    {
        Id = check.Id.Value,
        Origin = check.Origin.ToString(),
        Title = new Dictionary<string, string>(check.Title.Values),
        Description = check.Description is null ? null : new Dictionary<string, string>(check.Description.Values),
        Category = check.Category.ToString(),
        FromWeek = check.Window.FromWeek,
        ToWeek = check.Window.ToWeek,
        State = check.State.ToString(),
        Trimester = check.TrimesterOnly?.ToString(),
        Edited = check.IsEdited ? true : null,
        Appointment = ToDocument(check.Appointment),
        Kind = check.Kind?.ToString(),
    };

    private static AttachmentDocument ToDocument(ResultAttachment result) => new()
    {
        Id = result.Id.Value,
        Kind = result.Kind.ToString(),
        DisplayName = result.DisplayName,
        AddedAt = result.AddedAt,
        BlobId = result.BlobId,
        MediaType = result.MediaType,
        SizeBytes = result.SizeBytes,
    };

    private static ResultAttachment ToDomain(AttachmentDocument r) =>
        ResultAttachment.Rehydrate(
            new AttachmentId(r.Id),
            Parse<AttachmentKind>(r.Kind),
            r.DisplayName,
            r.AddedAt,
            r.BlobId,
            r.MediaType,
            r.SizeBytes);

    private static PlannedCheck ToDomain(CheckDocument d) =>
        PregnancyPlan.RehydrateCheck(
            new CheckId(d.Id),
            Parse<CheckOrigin>(d.Origin),
            new LocalizedText(d.Title),
            d.Description is null ? null : new LocalizedText(d.Description),
            Parse<CheckCategory>(d.Category),
            new WeekWindow(d.FromWeek, d.ToWeek),
            StateOf(d),
            ToDomain(d.Appointment),
            d.Trimester is null ? null : Parse<Trimester>(d.Trimester),
            d.Kind is null ? null : Parse<EntryKind>(d.Kind),

            // A plan saved before Maia kept this: one she booked or marked has been acted on.
            d.Edited == true || StateOf(d) != CheckState.NotBooked);

    /// <summary>
    /// The state a stored check is in. Older plans wrote "Pending" for not booked, and "Done" for a booking marked done
    /// after its day: that is a booking now, which shows as done once its time has passed, so its day and time stay.
    /// </summary>
    private static CheckState StateOf(CheckDocument d) => d.State switch
    {
        OldNotBookedState => CheckState.NotBooked,
        nameof(CheckState.Done) when d.Appointment is not null => CheckState.Booked,
        _ => Parse<CheckState>(d.State),
    };


    private static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Unknown {typeof(TEnum).Name} '{value}' in stored plan."));
}
