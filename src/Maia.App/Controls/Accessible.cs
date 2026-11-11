using Maia.Presentation.Accessibility;

namespace Maia.App.Controls;

/// <summary>
/// What TalkBack hears for the design's drawn controls: pills, switches, tick circles and icon-only buttons are borders
/// and layouts, not platform controls, so on their own they would be read as a name and nothing more.
/// <c>controls:Accessible.Role</c> says what the control is (a switch, a checkbox, a tab...) and
/// <c>controls:Accessible.IsOn</c> its state, spoken in the language Maia shows (<see cref="ScreenReaderText"/>). The
/// control's name stays its <c>SemanticProperties.Description</c>, which nothing here overwrites.
/// <para>
/// Things that appear after a tap are spoken too: what appears in place of the control she tapped takes the screen
/// reader's focus (<c>FocusWhenShown</c>), and a calm note, an error line or a toast is read out once that focus has
/// settled (<c>Announce</c>, <see cref="SayAfterFocus"/>), so the focus move does not cut it off.
/// </para>
/// </summary>
public static class Accessible
{
    public static readonly BindableProperty RoleProperty =
        BindableProperty.CreateAttached("Role", typeof(ControlRole), typeof(Accessible), ControlRole.None, propertyChanged: OnChanged);

    /// <summary>On, ticked off, selected, open or the current tab, by role.</summary>
    public static readonly BindableProperty IsOnProperty =
        BindableProperty.CreateAttached("IsOn", typeof(bool), typeof(Accessible), false, propertyChanged: OnChanged);

    /// <summary>
    /// For what appears in place of the control she tapped: a question asked in place (delete this task? remove this
    /// result?), a form opened by a button that hides itself (change the due date, back up, add a task), and that button
    /// again when the form or question closes. When it appears, the screen reader's focus moves to its first words, field
    /// or button, so she hears what appeared; where the focus was, the control she tapped has gone.
    /// </summary>
    public static readonly BindableProperty FocusWhenShownProperty =
        BindableProperty.CreateAttached("FocusWhenShown", typeof(bool), typeof(Accessible), false, propertyChanged: OnFocusWhenShownChanged);

    /// <summary>For a label that shows a calm note or an error: each new text it shows is read out once.</summary>
    public static readonly BindableProperty AnnounceProperty =
        BindableProperty.CreateAttached("Announce", typeof(bool), typeof(Accessible), false, propertyChanged: OnAnnounceChanged);

    /// <summary>Long enough for a view that has just appeared to be laid out before the screen reader goes to it.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Long enough for the focus moves a tap causes to be spoken first: the question or form that appeared (after
    /// <see cref="Settle"/>), or TalkBack's own move when the button she tapped went away (about 300 ms). TalkBack stops
    /// speaking when the focus moves, so a toast or a note said before them would be cut off.
    /// </summary>
    private static readonly TimeSpan AfterFocus = TimeSpan.FromMilliseconds(500);

    /// <summary>Says <paramref name="text"/> through the screen reader at once (a breathing phase, timed to the ring).</summary>
    public static void Say(string text) => SemanticScreenReader.Default.Announce(text);

    /// <summary>
    /// Says a toast or a calm note once the focus has settled after the tap that caused it, if
    /// <paramref name="stillShowing"/> is still true then: Save, then the toast, is heard as "Change due date, button",
    /// then the toast in full, instead of the toast cut off after a word.
    /// </summary>
    public static void SayAfterFocus(IDispatcher dispatcher, string text, Func<bool> stillShowing) =>
        dispatcher.DispatchDelayed(AfterFocus, () =>
        {
            if (stillShowing())
            {
                Say(text);
            }
        });

    public static ControlRole GetRole(BindableObject view) => (ControlRole)view.GetValue(RoleProperty);

    public static void SetRole(BindableObject view, ControlRole value) => view.SetValue(RoleProperty, value);

    public static bool GetIsOn(BindableObject view) => (bool)view.GetValue(IsOnProperty);

    public static void SetIsOn(BindableObject view, bool value) => view.SetValue(IsOnProperty, value);

    public static bool GetFocusWhenShown(BindableObject view) => (bool)view.GetValue(FocusWhenShownProperty);

    public static void SetFocusWhenShown(BindableObject view, bool value) => view.SetValue(FocusWhenShownProperty, value);

    public static bool GetAnnounce(BindableObject view) => (bool)view.GetValue(AnnounceProperty);

    public static void SetAnnounce(BindableObject view, bool value) => view.SetValue(AnnounceProperty, value);

    /// <summary>
    /// Puts the role and state on the platform view. Runs when either changes, and after MAUI maps a view's semantics
    /// (see MauiProgram), so a view that gets its handler later, or a new description, keeps them.
    /// </summary>
    internal static void Apply(VisualElement view)
    {
#if ANDROID
        Platforms.Android.AccessibleNodes.Apply(view);
#endif
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement view)
        {
            Apply(view);
        }
    }

    private static void OnFocusWhenShownChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement view || newValue is not true)
        {
            return;
        }

        // Only once it is on screen: a question already open when the screen is built is read in its place.
        var loaded = false;
        view.Loaded += (_, _) => loaded = true;
        view.Unloaded += (_, _) => loaded = false;
        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualElement.IsVisible) && loaded && view.IsVisible)
            {
                view.Dispatcher.DispatchDelayed(Settle, () => FocusFirstWords(view));
            }
        };
    }

    private static void OnAnnounceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Label label || newValue is not true)
        {
            return;
        }

        var loaded = false;
        label.Loaded += (_, _) => loaded = true;
        label.Unloaded += (_, _) => loaded = false;
        label.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Label.Text) && loaded && !string.IsNullOrWhiteSpace(label.Text))
            {
                // Still what it says then, and on the screen she is looking at (not a tab reloading behind it).
                var text = label.Text;
                SayAfterFocus(label.Dispatcher, text, () => label.Text == text && IsOnScreen(label));
            }
        };
    }

    /// <summary>
    /// Moves the screen reader to the first thing in <paramref name="view"/> it can stop on (words, a field, a button),
    /// if the view is still showing: a form opened in place starts with its first field or its title.
    /// </summary>
    private static void FocusFirstWords(VisualElement view)
    {
        if (!IsShown(view, upTo: null) || !IsOnScreen(view))
        {
            return;
        }

        var first = view.GetVisualTreeDescendants().OfType<VisualElement>().FirstOrDefault(element => IsReadable(element) && IsShown(element, upTo: view));
        if (first is null)
        {
            return;
        }

#if ANDROID
        Platforms.Android.AccessibleNodes.Focus(first);
#else
        var words = SemanticProperties.GetDescription(first) ?? (first as Label)?.Text ?? (first as Button)?.Text ?? (first as Entry)?.Placeholder;
        if (!string.IsNullOrWhiteSpace(words))
        {
            Say(words);
        }
#endif
    }

    /// <summary>Something the screen reader stops on: words, a button or a field, or one of the design's drawn controls.</summary>
    private static bool IsReadable(VisualElement element) =>
        AutomationProperties.GetIsInAccessibleTree(element) != false && element switch
        {
            Label label => !string.IsNullOrWhiteSpace(label.Text),
            Button or InputView or DatePicker or TimePicker or Picker => true,
            _ => GetRole(element) != ControlRole.None,
        };

    /// <summary>
    /// Whether <paramref name="element"/> and every layout around it is visible, up to <paramref name="upTo"/> or else
    /// up to its page: a button shown again inside a hidden screen state is not showing.
    /// </summary>
    private static bool IsShown(Element element, Element? upTo)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is VisualElement { IsVisible: false })
            {
                return false;
            }

            if (current == upTo || current is Page)
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>On the screen in front of her: its page is Shell's current one, or it is not on a page (the lock screen).</summary>
    private static bool IsOnScreen(Element element) =>
        Motions.FindPage(element) is not { } page || Shell.Current is not { } shell || shell.CurrentPage == page;
}
