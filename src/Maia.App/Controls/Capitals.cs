using System.Globalization;
using Maia.Presentation.Formatting;

namespace Maia.App.Controls;

/// <summary>
/// <c>controls:Capitals.Greek="True"</c> on an upper-cased label (the Eyebrow style sets it): in Greek, capitals
/// drop their accents, as Greek typography does ("ΕΠΟΜΕΝΟ", not "ΕΠΌΜΕΝΟ"). The platform upper-cases with accents,
/// so the finished text is replaced right after it is shown.
/// </summary>
public static class Capitals
{
    public static readonly BindableProperty GreekProperty =
        BindableProperty.CreateAttached("Greek", typeof(bool), typeof(Capitals), false, propertyChanged: OnGreekChanged);

    public static bool GetGreek(BindableObject view) => (bool)view.GetValue(GreekProperty);

    public static void SetGreek(BindableObject view, bool value) => view.SetValue(GreekProperty, value);

    private static void OnGreekChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Label label || newValue is not true)
        {
            return;
        }

        label.HandlerChanged += (_, _) => Apply(label);
        label.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Label.Text) or nameof(Label.TextTransform))
            {
                Apply(label);
            }
        };
    }

    private static void Apply(Label label)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (label.Text is not { } text || culture.TwoLetterISOLanguageName != Maia.Presentation.Localization.Text.Greek)
        {
            return;
        }

        // After the platform has set its own (accented) capitals.
        label.Dispatcher.Dispatch(() =>
        {
#if ANDROID
            if (label.Handler?.PlatformView is Android.Widget.TextView view && label.Text == text)
            {
                view.Text = DisplayFormat.Caps(text, culture);
            }
#endif
        });
    }
}
