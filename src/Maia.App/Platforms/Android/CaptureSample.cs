#if DEBUG
using Android.Content;
using Android.Graphics;
using Android.Graphics.Pdf;
using Maia.App.Controls;
using Maia.App.Services;
using Maia.Application.Abstractions;
using Microsoft.Maui.Platform;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Plans;
using Maia.Presentation;
using Maia.Presentation.Localization;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Debug builds only: the sample journey the design's screenshots are captured with (docs/design/README.md,
/// Screenshots), and a way for the capture script to open any screen. Never in a store build.
/// <code>
/// adb shell am start -n com.michailgeranis.maia/crc….MainActivity --es maia.capture sample
/// adb shell am start -n com.michailgeranis.maia/crc….MainActivity --es maia.capture //main/plan
/// </code>
/// The sample is made fresh each time: today is week 24 + 3, her OB-GYN is "Dr. Eleni Papadaki", with a booking,
/// entries she added (one with a note), this week's tasks and one for a later week, questions and two results. Nothing
/// in it is hers or anyone's.
/// </summary>
internal static class CaptureSample
{
    public const string Extra = "maia.capture";

    public static void Receive(Intent? intent)
    {
        var what = intent?.GetStringExtra(Extra);
        if (string.IsNullOrEmpty(what) || IPlatformApplication.Current?.Services is not { } services)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (what == "sample")
            {
                await MakeAsync(services.GetRequiredService<PlanService>(), services.GetRequiredService<IClock>());
                await ShellNavigator.Current!.GoToAsync("//main/settings", animate: false);
                await ShellNavigator.Current!.GoToAsync("//main/week", animate: false);
            }
            else
            {
                await ShellNavigator.Current!.GoToAsync(what, animate: false);
            }
        });
    }

    private static async Task MakeAsync(PlanService plans, IClock clock)
    {
        await plans.DeleteEverythingAsync();
        var today = clock.Today;

        // Week 24 + 3 today: 280 days in all, 171 behind her.
        await plans.CreatePlanAsync(new CreatePlanRequest(today.AddDays(280 - 171), AppConfig.DefaultCountry, Text.Language));
        await plans.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 210 555 2210", "Mobile", null)]);

        var plan = await plans.GetPlanAsync();
        var doctor = plan.CareTeam.Contacts.First(contact => contact.Role == CareRole.ObGyn).Id;
        var next = plan.Checks.FirstOrDefault(check => !check.IsMilestone && check.State == CheckState.NotBooked && check.Window.ToWeek >= 24);
        if (next is not null)
        {
            await plans.BookCheckAsync(next.Id, new Appointment(today.AddDays(2), new TimeOnly(10, 30), with: doctor));
        }

        await plans.AddEntryAsync("Hospital tour", EntryKind.HospitalVisit, EntryWhen.Booked(new Appointment(today.AddDays(23), new TimeOnly(17, 0))));
        await plans.AddEntryAsync("Growth scan", EntryKind.Exam, EntryWhen.NotBookedInWeek(28), "Bring the report from the last scan.");
        await plans.AddEntryAsync("Dentist check-up", EntryKind.Other, EntryWhen.DoneInWeek(22));

        await plans.AddTaskAsync("Call the clinic about the glucose test", today.AddDays(1), new TimeOnly(9, 0));
        await plans.AddTaskAsync("Order the vitamins", today.AddDays(2), new TimeOnly(18, 0));
        await plans.AddTaskAsync("Pack the hospital bag list", today.AddDays(4));
        await plans.AddTaskAsync("Ask work about leave dates", week: 26);

        // Two blood pressure readings, small on Journey before "You are here".
        await plans.LogBloodPressureAsync(118, 76, today.AddDays(-1), new TimeOnly(8, 30));
        await plans.LogBloodPressureAsync(121, 78, today, new TimeOnly(7, 45));

        // Each new question goes on top: written last first, they read swimming, classes, hospital.
        var answered = await plans.AddQuestionAsync("Which hospital do you deliver at?");
        await plans.AddQuestionAsync("When should I book the antenatal classes?");
        await plans.AddQuestionAsync("Can I keep swimming twice a week?");
        await plans.SetQuestionAnsweredAsync(answered, answered: true);

        await plans.AttachFileAsync(new FileToAttach("20-week scan.png", "image/png", ScanPhoto()));
        await plans.AttachFileAsync(new FileToAttach("Blood tests.pdf", "application/pdf", Report()));
    }

    /// <summary>A soft drawing that stands for a scan photo: no picture of anyone.</summary>
    private static byte[] ScanPhoto()
    {
        using var bitmap = Bitmap.CreateBitmap(900, 1200, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(Palette.Get("SkyDeep").ToPlatform());
        using var paint = new global::Android.Graphics.Paint { AntiAlias = true, Color = Palette.Get("Sky").ToPlatform() };
        canvas.DrawOval(new global::Android.Graphics.RectF(220, 360, 680, 840), paint);
        using var stream = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Png!, 90, stream);
        return stream.ToArray();
    }

    /// <summary>A one-page report with a few plain lines, as a PDF.</summary>
    private static byte[] Report()
    {
        using var document = new PdfDocument();
        var page = document.StartPage(new PdfDocument.PageInfo.Builder(595, 842, 1).Create())!;
        using var paint = new global::Android.Graphics.Paint { AntiAlias = true, TextSize = 18, Color = Palette.Get("Ink").ToPlatform() };
        var sheet = page.Canvas!;
        sheet.DrawText("Blood tests", 60, 90, paint);
        paint.TextSize = 12;
        for (var line = 0; line < 12; line++)
        {
            sheet.DrawText("Sample line of a laboratory report", 60, 140 + (line * 28), paint);
        }

        document.FinishPage(page);
        using var stream = new MemoryStream();
        document.WriteTo(stream);
        return stream.ToArray();
    }
}
#endif
