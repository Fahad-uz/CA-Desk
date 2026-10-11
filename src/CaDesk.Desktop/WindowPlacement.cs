namespace CaDesk.Desktop;

internal readonly record struct PixelBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

internal static class WindowPlacement
{
    public static int Gap(PixelBounds work, double scale) => Math.Min((int)Math.Ceiling(16 * scale), Math.Max(0, Math.Min(work.Width, work.Height) / 4));

    public static (double Width, double Height) MinimumSize(PixelBounds work, double scale)
    {
        var gap = Gap(work, scale);
        return (Math.Min(1000, (work.Width - 2 * gap) / scale), Math.Min(680, (work.Height - 2 * gap) / scale));
    }

    public static PixelBounds Fit(PixelBounds window, PixelBounds work, double scale, bool center)
    {
        var gap = Gap(work, scale);
        var width = Math.Clamp(window.Width, 1, Math.Max(1, work.Width - 2 * gap));
        var height = Math.Clamp(window.Height, 1, Math.Max(1, work.Height - 2 * gap));
        var left = center ? work.Left + (work.Width - width) / 2 : Math.Clamp(window.Left, work.Left + gap, work.Right - gap - width);
        var top = center ? work.Top + (work.Height - height) / 2 : Math.Clamp(window.Top, work.Top + gap, work.Bottom - gap - height);
        return new(left, top, width, height);
    }

    public static (double Left, double Top, double Right, double Bottom) Insets(PixelBounds client, PixelBounds work, double scale) =>
        (Math.Max(0, work.Left - client.Left) / scale, Math.Max(0, work.Top - client.Top) / scale,
         Math.Max(0, client.Right - work.Right) / scale, Math.Max(0, client.Bottom - work.Bottom) / scale);
}
