using Android.App;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using AndroidX.Activity;
using AndroidX.Core.View;
using Maia.App.Controls;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using MauiView = Microsoft.Maui.Controls.View;

namespace Maia.App.Platforms.Android;

/// <summary>
/// The lock screen's own window: a full-screen dialog over Maia's activity. It is not a page on Shell's navigation, so
/// the page underneath never sees it come and go: the result viewer keeps her file open, Add keeps what she chose and
/// typed, breathing carries on, and the start-up screen doesn't open her plan a second time. Back leaves Maia locked,
/// in the background.
/// </summary>
public sealed class LockWindow : ComponentDialog
{
    private readonly FrameLayout _frame;

    private LockWindow(Activity activity, MauiView content, IMauiContext context)
        : base(activity, Resource.Style.Maui_MainTheme_NoActionBar)
    {
        Content = content;
        SetCancelable(false);

        // Her pages show through only while it fades out on unlocking; the lock screen itself is opaque.
        _frame = new FrameLayout(activity);
        _frame.SetBackgroundColor(Palette.Get("Mist").ToPlatform());
        _frame.AddView(content.ToPlatform(context), new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        ViewCompat.SetOnApplyWindowInsetsListener(_frame, new SystemBarsPadding());
        SetContentView(_frame, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        if (Window is { } window)
        {
            window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            window.SetBackgroundDrawable(new ColorDrawable(global::Android.Graphics.Color.Transparent));
            window.ClearFlags(WindowManagerFlags.DimBehind);

            // Up at once: nothing of her plan may show while it would slide or fade in.
            window.SetWindowAnimations(0);
        }

        OnBackPressedDispatcher.AddCallback(this, new LeaveLocked(activity));
    }

    /// <summary>The lock screen shown in this window.</summary>
    public MauiView Content { get; }

    /// <summary>Shows <paramref name="content"/> over everything in <paramref name="activity"/>.</summary>
    public static LockWindow Open(Activity activity, MauiView content, IMauiContext context)
    {
        var window = new LockWindow(activity, content, context);
        window.Show();
        return window;
    }

    /// <summary>Takes the lock screen away, fading when the phone allows motion, and lets go of its view.</summary>
    public void Close(bool fade)
    {
        if (!fade || !IsShowing)
        {
            CloseNow();
            return;
        }

        _frame.Animate()?.Alpha(0).SetDuration(150).WithEndAction(new Java.Lang.Runnable(CloseNow));
    }

    private void CloseNow()
    {
        if (IsShowing && OwnerActivity is not { IsDestroyed: true })
        {
            Dismiss();
        }

        Content.DisconnectHandlers();
    }

    /// <summary>Back: Maia goes to the background as it is, locked, instead of closing the lock screen.</summary>
    private sealed class LeaveLocked(Activity activity) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed() => activity.MoveTaskToBack(true);
    }

    /// <summary>Keeps the lock screen's content clear of the status and navigation bars when Android draws edge to edge.</summary>
    private sealed class SystemBarsPadding : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(AView? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets?.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout()) is not { } bars)
            {
                return insets;
            }

            v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}
