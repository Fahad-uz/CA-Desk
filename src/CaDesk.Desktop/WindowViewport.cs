using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CaDesk.Desktop;

// Work-area rectangles and HWND bounds are physical pixels; WPF sizes/margins are DIPs.
internal sealed class WindowViewport
{
    private readonly Window window;
    private readonly FrameworkElement content;
    private HwndSource? source;
    private IntPtr handle;
    private bool pending, fitPending, closed;

    public WindowViewport(Window window, FrameworkElement content)
    {
        this.window = window;
        this.content = content;
        window.SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            source?.AddHook(OnMessage);
            if (TryMonitor(handle, out var work, out var scale))
            {
                ApplyMinimums(work, scale);
                var gap = WindowPlacement.Gap(work, scale);
                window.Width = Math.Min(window.Width, (work.Width - 2 * gap) / scale);
                window.Height = Math.Min(window.Height, (work.Height - 2 * gap) / scale);
            }
        };
        // WPF has selected and centered the startup monitor by Loaded. Correct the
        // native rectangle before the user needs to minimize, restore or drag it.
        window.Loaded += (_, _) => { FitNormal(true); UpdateInsets(); };
        window.StateChanged += (_, _) => Schedule(false);
        window.Closed += (_, _) => { closed = true; source?.RemoveHook(OnMessage); };
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0005) Schedule(false); // WM_SIZE: maximize/restore frame changes.
        else if (message is 0x02E0 or 0x007E or 0x001A) Schedule(true); // DPI, display or work area changed.
        return IntPtr.Zero; // WPF/WindowChrome retain native sizing, dragging and snapping.
    }

    private void Schedule(bool fit)
    {
        fitPending |= fit;
        if (pending || closed) return;
        pending = true;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            pending = false;
            if (closed) return;
            var shouldFit = fitPending; fitPending = false;
            if (shouldFit) FitNormal(false);
            UpdateInsets();
        }));
    }

    private void ApplyMinimums(PixelBounds work, double scale)
    {
        var min = WindowPlacement.MinimumSize(work, scale);
        window.MinWidth = min.Width;
        window.MinHeight = min.Height;
    }

    private void FitNormal(bool center)
    {
        if (!TryMonitor(handle, out var work, out var scale)) return;
        ApplyMinimums(work, scale);
        if (window.WindowState != WindowState.Normal || !GetWindowRect(handle, out var rect)) return;
        var current = rect.Bounds;
        var fitted = WindowPlacement.Fit(current, work, scale, center);
        if (fitted != current)
            SetWindowPos(handle, IntPtr.Zero, fitted.Left, fitted.Top, fitted.Width, fitted.Height, 0x0014);
    }

    private void UpdateInsets()
    {
        var margin = new Thickness(0);
        if (window.WindowState == WindowState.Maximized && TryMonitor(handle, out var work, out var scale))
        {
            window.UpdateLayout();
            // Measure the content itself: WindowChrome and the native frame can
            // each offset it. Remove our current margin to avoid accumulating padding.
            var first = content.PointToScreen(new Point(0, 0));
            var last = content.PointToScreen(new Point(content.ActualWidth, content.ActualHeight));
            var left = (int)Math.Floor(first.X - content.Margin.Left * scale);
            var top = (int)Math.Floor(first.Y - content.Margin.Top * scale);
            var right = (int)Math.Ceiling(last.X + content.Margin.Right * scale);
            var bottom = (int)Math.Ceiling(last.Y + content.Margin.Bottom * scale);
            var inset = WindowPlacement.Insets(new(left, top, right - left, bottom - top), work, scale);
            margin = new(inset.Left, inset.Top, inset.Right, inset.Bottom);
        }
        content.Margin = margin;
    }

    internal static bool CaptionIsOnScreen(Window window)
    {
        window.UpdateLayout();
        if (!TryMonitor(new WindowInteropHelper(window).Handle, out var work, out _)) return false;
        foreach (var name in new[] { "MinimizeButton", "MaximizeRestoreButton", "CloseButton" })
        {
            if (window.FindName(name) is not Button button || !button.IsVisible || button.ActualWidth <= 0 || button.ActualHeight <= 0) return false;
            var first = button.PointToScreen(new Point(0, 0));
            var last = button.PointToScreen(new Point(button.ActualWidth, button.ActualHeight));
            if (first.X < work.Left - 1 || first.Y < work.Top - 1 || last.X > work.Right + 1 || last.Y > work.Bottom + 1) return false;
            var hit = window.InputHitTest(window.PointFromScreen(button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2)))) as DependencyObject;
            while (hit != null && hit != button) hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            if (hit != button) return false;
        }
        return true;
    }

    internal static string CaptionDiagnostics(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        TryMonitor(hwnd, out var work, out var scale);
        GetWindowRect(hwnd, out var native);
        var info = new List<string> { $"state={window.WindowState}; size={window.Width}x{window.Height}; actual={window.ActualWidth}x{window.ActualHeight}; native={native.Bounds}; work={work}; scale={scale}" };
        foreach (var name in new[] { "MinimizeButton", "MaximizeRestoreButton", "CloseButton" })
        {
            var button = (Button)window.FindName(name);
            var first = button.PointToScreen(new Point(0, 0));
            var last = button.PointToScreen(new Point(button.ActualWidth, button.ActualHeight));
            var hit = window.InputHitTest(window.PointFromScreen(button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2))));
            info.Add($"{name}: visible={button.IsVisible}; {first}..{last}; hit={hit?.GetType().Name ?? "null"}");
        }
        return string.Join(" | ", info);
    }

    private static bool TryMonitor(IntPtr hwnd, out PixelBounds work, out double scale)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var ok = GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info);
        work = info.Work.Bounds;
        var dpi = GetDpiForWindow(hwnd);
        scale = dpi > 0 ? dpi / 96.0 : 1;
        return ok && work.Width > 0 && work.Height > 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelBounds Bounds => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
