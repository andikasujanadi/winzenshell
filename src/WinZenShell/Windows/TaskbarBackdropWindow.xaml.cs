using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinZenShell.Native;

namespace WinZenShell.Windows;

public partial class TaskbarBackdropWindow : Window
{
    private IntPtr _hwnd = IntPtr.Zero;

    public IntPtr Handle => _hwnd;

    public TaskbarBackdropWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        int exStyle = Win32Api.GetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_TOPMOST | Win32Api.WS_EX_TRANSPARENT | Win32Api.WS_EX_NOACTIVATE;
        exStyle &= ~Win32Api.WS_EX_APPWINDOW;
        Win32Api.SetWindowLong(_hwnd, Win32Api.GWL_EXSTYLE, exStyle);
    }

    public void UpdateBackdropAppearance(bool isLight, bool isFloating, bool isTop, int radiusPx)
    {
        OuterBorder.Background = isLight
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x1C));

        OuterBorder.BorderBrush = isLight
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDF, 0xDF, 0xDF))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x2E, 0x2E));

        if (isFloating)
        {
            double w = !double.IsNaN(Width) && Width > 0 ? Width : ActualWidth;
            double h = !double.IsNaN(Height) && Height > 0 ? Height : ActualHeight;
            double maxR = Math.Max(0.0, Math.Min(w, h) / 2.0);
            double effR = Math.Min(Math.Max(0, radiusPx), maxR);
            OuterBorder.CornerRadius = new CornerRadius(effR);
            OuterBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            OuterBorder.CornerRadius = new CornerRadius(0);
            OuterBorder.BorderThickness = isTop ? new Thickness(0, 0, 0, 1) : new Thickness(0, 1, 0, 0);
        }
    }

    public void SyncPositionBehind(IntPtr taskbarHwnd, double dipLeft, double dipTop, double dipWidth, double dipHeight)
    {
        Left = dipLeft;
        Top = dipTop;
        Width = dipWidth;
        Height = dipHeight;
        UpdateLayout();

        if (_hwnd != IntPtr.Zero && taskbarHwnd != IntPtr.Zero && Win32Api.GetWindowRect(taskbarHwnd, out var rc))
        {
            Win32Api.SetWindowPos(
                _hwnd,
                taskbarHwnd,
                rc.Left,
                rc.Top,
                rc.Width,
                rc.Height,
                Win32Api.SWP_NOACTIVATE);
        }
    }

    public void CaptureToFile(string outPath)
    {
        try
        {
            UpdateLayout();
            int w = Math.Max(1, (int)Math.Ceiling(ActualWidth));
            int h = Math.Max(1, (int)Math.Ceiling(ActualHeight));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(fs);
        }
        catch { }
    }
}
