using Android.App;
using Android.Content.Res;
using Maia.Presentation.Localization;

namespace Maia.App.Platforms.Android;

/// <summary>
/// The phone's calendar that opens from a date field speaks the language Maia shows. Android draws it in the phone's
/// language, so with Maia in Greek on an English phone it read "Sat, Oct 3" among Greek words. In English it stays the
/// phone's own, as Maia's English keeps her phone's way of writing dates.
/// </summary>
internal sealed class DateFieldHandler : Microsoft.Maui.Handlers.DatePickerHandler
{
    protected override DatePickerDialog CreateDatePickerDialog(int year, int month, int day)
    {
        if (Text.Language != Text.Greek || Context.Resources?.Configuration is not { } phone)
        {
            return base.CreateDatePickerDialog(year, month, day);
        }

        var locale = Java.Util.Locale.ForLanguageTag("el-GR");
        var greek = new Configuration(phone);
        greek.SetLocale(locale);

        // The screen itself, with Greek laid over its words: a context made from the configuration alone has no window,
        // and Android won't show a dialog on it.
        var context = new global::Android.Views.ContextThemeWrapper(Context, Context.Theme);
        context.ApplyOverrideConfiguration(greek);

        // The calendar takes the language of its heading ("Κυρ, 21 Φεβ") from the default as it is made; the phone's
        // comes back at once.
        var phoneLocale = Java.Util.Locale.Default;
        DatePickerDialog dialog;
        try
        {
            Java.Util.Locale.Default = locale;
            dialog = new DatePickerDialog(context, (_, e) =>
            {
                if (VirtualView is { } view)
                {
                    view.Date = e.Date;
                }
            }, year, month, day);
        }
        finally
        {
            Java.Util.Locale.Default = phoneLocale;
        }

        // As MAUI's own: once it closes, the field knows it is closed (and lets go of the dialog).
        dialog.DismissEvent += (_, _) =>
        {
            if (VirtualView is { } view)
            {
                view.IsOpen = false;
            }
        };
        return dialog;
    }
}
