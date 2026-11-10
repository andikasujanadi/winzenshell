using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace WinZenShell.Native;

public static class FlyoutAnimationHelper
{
    private sealed class AnimState
    {
        public int Token;
        public DateTime LastShownUtc = DateTime.MinValue;
        public bool IsHiding;
        public double RestTop;
        public string LastDockEdge = "Bottom";
        public bool LastUseOpacityFade = true;
    }

    private static readonly ConditionalWeakTable<Window, AnimState> _states = new();

    public static bool AnimationsEnabled { get; set; } = true;
    public static IntPtr TaskbarHwnd { get; set; } = IntPtr.Zero;

    private static AnimState GetState(Window w) => _states.GetOrCreateValue(w);

    public static void AnimateShow(
        Window window,
        double finalTop,
        string dockEdge,
        bool activate = true,
        bool useOpacityFade = true)
    {
        var state = GetState(window);
        int token = ++state.Token;
        state.IsHiding = false;
        state.LastShownUtc = DateTime.UtcNow;
        state.RestTop = finalTop;
        state.LastDockEdge = dockEdge ?? "Bottom";
        state.LastUseOpacityFade = useOpacityFade;

        window.BeginAnimation(Window.TopProperty, null);
        window.BeginAnimation(Window.OpacityProperty, null);

        if (!AnimationsEnabled)
        {
            window.Top = finalTop;
            window.Opacity = 1.0;
            if (!window.IsVisible) window.Show();
            if (activate)
            {
                window.Activate();
                window.Focus();
            }
            return;
        }

        double offset = string.Equals(state.LastDockEdge, "Top", StringComparison.OrdinalIgnoreCase) ? -10.0 : 10.0;
        double startTop = finalTop + offset;

        // Apply starting frame values BEFORE Show() so Frame 1 never flashes at finalTop or Opacity=1
        window.Top = startTop;
        window.Opacity = useOpacityFade ? 0.0 : 1.0;

        var slideAnim = new DoubleAnimation(startTop, finalTop, new Duration(TimeSpan.FromMilliseconds(165)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        slideAnim.Completed += (_, _) =>
        {
            if (state.Token != token) return;
            window.BeginAnimation(Window.TopProperty, null);
            window.Top = finalTop;
        };
        window.BeginAnimation(Window.TopProperty, slideAnim, HandoffBehavior.SnapshotAndReplace);

        if (useOpacityFade)
        {
            var fadeAnim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(140)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            fadeAnim.Completed += (_, _) =>
            {
                if (state.Token != token) return;
                window.BeginAnimation(Window.OpacityProperty, null);
                window.Opacity = 1.0;
            };
            window.BeginAnimation(Window.OpacityProperty, fadeAnim, HandoffBehavior.SnapshotAndReplace);
        }

        if (!window.IsVisible) window.Show();
        if (activate)
        {
            window.Activate();
            window.Focus();
        }
    }

    public static void AnimateHide(Window window, Action? afterHide = null, string? dockEdge = null)
    {
        var state = GetState(window);
        if (!string.IsNullOrWhiteSpace(dockEdge))
            state.LastDockEdge = dockEdge;

        if (!window.IsVisible)
        {
            state.IsHiding = false;
            afterHide?.Invoke();
            return;
        }

        if (state.IsHiding) return;

        if (!AnimationsEnabled)
        {
            state.Token++;
            state.IsHiding = false;
            window.BeginAnimation(Window.TopProperty, null);
            window.BeginAnimation(Window.OpacityProperty, null);
            window.Opacity = 1.0;
            window.Hide();
            afterHide?.Invoke();
            return;
        }

        int token = ++state.Token;
        state.IsHiding = true;

        double curTop = window.Top;
        double offset = string.Equals(state.LastDockEdge, "Top", StringComparison.OrdinalIgnoreCase) ? -8.0 : 8.0;
        double endTop = curTop + offset;

        var slideOut = new DoubleAnimation(curTop, endTop, new Duration(TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        slideOut.Completed += (_, _) =>
        {
            if (state.Token != token) return;
            state.IsHiding = false;
            window.BeginAnimation(Window.TopProperty, null);
            window.BeginAnimation(Window.OpacityProperty, null);
            window.Hide();
            window.Top = state.RestTop;
            window.Opacity = 1.0;
            afterHide?.Invoke();
        };
        window.BeginAnimation(Window.TopProperty, slideOut, HandoffBehavior.SnapshotAndReplace);

        if (state.LastUseOpacityFade)
        {
            var fadeOut = new DoubleAnimation(window.Opacity, 0.0, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd
            };
            window.BeginAnimation(Window.OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
        }
    }

    public static void HandleDeactivated(Window window, Action recordDeactivatedTime, Action? afterHide = null)
    {
        var state = GetState(window);
        if (state.IsHiding || !window.IsVisible) return;

        // Ignore transient focus bounce back to TaskbarWebWindow immediately after opening another icon's flyout
        if ((DateTime.UtcNow - state.LastShownUtc).TotalMilliseconds < 320)
        {
            IntPtr fg = Win32Api.GetForegroundWindow();
            if (fg == IntPtr.Zero || (TaskbarHwnd != IntPtr.Zero && fg == TaskbarHwnd))
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (window.IsVisible && !state.IsHiding)
                    {
                        try
                        {
                            window.Activate();
                            window.Focus();
                        }
                        catch { }
                    }
                }));
                return;
            }
        }

        recordDeactivatedTime();
        AnimateHide(window, afterHide);
    }

    public static void AnimateTaskbarFlip(Window taskbar, double finalTop, bool isTopEdge)
    {
        var state = GetState(taskbar);
        int token = ++state.Token;
        taskbar.BeginAnimation(Window.TopProperty, null);

        if (!AnimationsEnabled)
        {
            taskbar.Top = finalTop;
            return;
        }

        double startTop = isTopEdge ? (finalTop - 20.0) : (finalTop + 20.0);
        taskbar.Top = startTop;

        var slideAnim = new DoubleAnimation(startTop, finalTop, new Duration(TimeSpan.FromMilliseconds(210)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        slideAnim.Completed += (_, _) =>
        {
            if (state.Token != token) return;
            taskbar.BeginAnimation(Window.TopProperty, null);
            taskbar.Top = finalTop;
        };
        taskbar.BeginAnimation(Window.TopProperty, slideAnim, HandoffBehavior.SnapshotAndReplace);
    }
}
