using Maia.Application.Common;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Common;

/// <summary>
/// Calm, specific words for a failure a screen can meet, chosen by its stable code (<see cref="DomainException.Code"/>
/// or <see cref="MaiaApplicationException.Code"/>). A code with no words of its own, and any other failure, gets the
/// general "Something went wrong". Only the code picks the text: nothing from the exception's message is ever shown.
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// Codes that only a fault in Maia or its templates can raise: nothing she does leads to them, so they keep the
    /// general message. A test holds every code in the source to either words of its own or this list.
    /// </summary>
    public static readonly IReadOnlySet<string> Internal = new HashSet<string>(StringComparer.Ordinal)
    {
        "domain.unknown",
        "app.unknown",
        "check.id.invalid",
        "text.english.missing",
        "window.invalid",
        "plan.override.unknown_check",
        "attachment.size.invalid",
        "template.empty",
        "template.check.duplicate",
        "template.check.id_reserved",
        "template.country.unsupported",

        // What an entry is comes from Maia's own choices on screen, never from anything she types.
        "entry.kind.invalid",

        // When an entry is comes from the pickers on screen: a trimester, a week from 1 to 42, or a day and time.
        "entry.trimester.invalid",
        "entry.week.invalid",
        "entry.when.invalid",

        // When a task is comes from the same pickers: a day and time, a trimester or a week, never two of them.
        "task.when.invalid",

        // The home time zone comes from the phone, never from anything she types.
        "plan.time_zone.invalid",
    };

    /// <summary>What to tell her about <paramref name="exception"/>.</summary>
    public static string For(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var specific = exception switch
        {
            BackupUnreadableException backup => BackupMessages.For(backup),
            DomainException domain => ForCode(domain.Code),
            MaiaApplicationException application => ForCode(application.Code),
            _ => null,
        };
        return specific ?? Text.Get("Common_Error");
    }

    /// <summary>The words for <paramref name="code"/>, or null when it has none of its own.</summary>
    public static string? ForCode(string code) => code switch
    {
        "care_team.role_full" => Text.Format("Error_RoleFull", CareTeam.MaxContactsPerRole),
        "care_team.contact_not_found" or "appointment.contact_unknown" => Text.Get("Error_ContactGone"),
        "contact.field.too_long" => Text.Get("Error_ContactTooLong"),
        "check.is_milestone" => Text.Get("Error_DueDateNotACheck"),
        "check.not_found" => Text.Get("Error_CheckGone"),
        "plan.checks.full" => Text.Format("Error_ChecksFull", PregnancyPlan.MaxChecks),
        "plan.tasks.full" => Text.Format("Error_TasksFull", PregnancyPlan.MaxTasks),
        "plan.questions.full" => Text.Format("Error_QuestionsFull", PregnancyPlan.MaxQuestions),
        "task.title.too_long" => Text.Format("Error_TaskTooLong", PlanTask.MaxTitleLength),
        "entry.title.too_long" => Text.Format("Error_EntryTooLong", PlannedCheck.MaxTitleLength),
        "entry.note.too_long" => Text.Format("Error_NoteTooLong", PlannedCheck.MaxNoteLength),
        "task.time_without_date" => Text.Get("Error_TaskNeedsDay"),
        "task.when.missing" => Text.Get("Add_ChooseWhen"),
        "task.not_found" => Text.Get("Error_TaskGone"),
        "question.text.too_long" => Text.Format("Error_QuestionTooLong", DoctorQuestion.MaxTextLength),
        "question.not_found" => Text.Get("Error_QuestionGone"),
        "plan.readings.full" => Text.Format("Error_ReadingsFull", PregnancyPlan.MaxReadings),
        "reading.values.invalid" => Text.Get("Error_ReadingInvalid"),
        "reading.not_found" => Text.Get("Error_ReadingGone"),
        "appointment.location.too_long" => Text.Format("Error_PlaceTooLong", Appointment.MaxLocationLength),
        "attachment.duplicate" => Text.Get("Error_ResultFiled"),
        "attachment.not_found" => Text.Get("Error_ResultGone"),
        "attachment.not_a_file" => Text.Get("Error_ResultNoFile"),
        "attachment.type.unsupported" => Text.Get("Results_Unsupported"),
        "attachment.size.too_large" => Text.Get("Results_TooLarge"),
        "attachment.size.empty" => Text.Get("Results_Empty"),
        "dating.out_of_range" => Text.Get("Setup_OutOfRange"),
        "template.not_reviewed" => Text.Get("SetupPlan_NotReviewed"),
        "plan.not_found" => Text.Get("Error_NoPlan"),
        "plan.already_exists" => Text.Get("Error_PlanExists"),
        PlanUnreadableException.NewerAppCode => Text.Get("Error_PlanFromNewerMaia"),
        PlanUnreadableException.KeyUnavailableCode or PlanUnreadableException.DamagedCode => Text.Get("Error_PlanUnreadable"),
        BackupUnreadableException.WrongPasswordCode => Text.Get("Backup_WrongPassword"),
        BackupUnreadableException.NotABackupCode => Text.Get("Backup_NotABackup"),
        BackupUnreadableException.NewerAppCode => Text.Get("Backup_NewerApp"),
        BackupUnreadableException.DamagedCode => Text.Get("Backup_Damaged"),
        _ => null,
    };
}
