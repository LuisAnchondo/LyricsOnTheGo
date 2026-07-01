using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyricsOnTheGo.Controls;

/// <summary>
/// Compact inline HSV colour picker (saturation/value square + hue bar + hex). Raises
/// <see cref="ColorChanged"/> with a "#RRGGBB" string. Used inside a Popup so no separate
/// OS dialog is needed.
/// </summary>
public partial class ColorPicker : UserControl
{
    private const double SvW = 190, SvH = 120, HueW = 190;

    private double _h;     // 0–360
    private double _s;     // 0–1
    private double _v;     // 0–1
    private bool _suppressHex;
    private bool _draggingSv, _draggingHue;

    public event Action<string>? ColorChanged;

    public ColorPicker()
    {
        InitializeComponent();
    }

    /// <summary>Seeds the picker from a hex string without raising ColorChanged.</summary>
    public void SetColor(string? hex)
    {
        if (TryParse(hex, out var c))
        {
            (_h, _s, _v) = RgbToHsv(c);
            Render(raise: false, updateHex: true);
        }
    }

    private void Render(bool raise, bool updateHex)
    {
        HueRect.Fill = new SolidColorBrush(HsvToColor(_h, 1, 1));
        Canvas.SetLeft(SvThumb, _s * SvW - SvThumb.Width / 2);
        Canvas.SetTop(SvThumb, (1 - _v) * SvH - SvThumb.Height / 2);
        Canvas.SetLeft(HueThumb, _h / 360.0 * HueW - HueThumb.Width / 2);

        var c = HsvToColor(_h, _s, _v);
        string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        if (updateHex)
        {
            _suppressHex = true;
            HexInput.Text = hex;
            _suppressHex = false;
        }
        if (raise)
            ColorChanged?.Invoke(hex);
    }

    private void SvDown(object sender, MouseButtonEventArgs e) { _draggingSv = true; SvArea.CaptureMouse(); UpdateSv(e.GetPosition(SvArea)); }
    private void SvMove(object sender, MouseEventArgs e) { if (_draggingSv) UpdateSv(e.GetPosition(SvArea)); }
    private void SvUp(object sender, MouseButtonEventArgs e) { _draggingSv = false; SvArea.ReleaseMouseCapture(); }
    private void UpdateSv(Point p) { _s = Clamp01(p.X / SvW); _v = 1 - Clamp01(p.Y / SvH); Render(raise: true, updateHex: true); }

    private void HueDown(object sender, MouseButtonEventArgs e) { _draggingHue = true; HueArea.CaptureMouse(); UpdateHue(e.GetPosition(HueArea)); }
    private void HueMove(object sender, MouseEventArgs e) { if (_draggingHue) UpdateHue(e.GetPosition(HueArea)); }
    private void HueUp(object sender, MouseButtonEventArgs e) { _draggingHue = false; HueArea.ReleaseMouseCapture(); }
    private void UpdateHue(Point p) { _h = Clamp01(p.X / HueW) * 360; Render(raise: true, updateHex: true); }

    // ---- Eyedropper (pick a colour from anywhere on screen) ---------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hDC, int x, int y);

    private void OnEyedropper(object sender, RoutedEventArgs e)
    {
        // A transparent overlay spanning all monitors captures the next click; we read the pixel
        // under the (physical) cursor with GetPixel on the screen DC. A small swatch follows the
        // cursor showing the colour live as you hover (like the previous project).
        var overlay = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Cursor = Cursors.Cross,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
        };

        var swatch = new System.Windows.Shapes.Ellipse
        {
            Width = 46, Height = 46, Stroke = Brushes.White, StrokeThickness = 3,
            Fill = Brushes.Black,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.6 },
        };
        var hexChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 2, 5, 2),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
            Child = new TextBlock
            {
                Foreground = Brushes.White, FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 11,
            },
        };
        var hexLabel = (TextBlock)hexChip.Child;
        var follower = new StackPanel { Children = { swatch, hexChip } };
        var canvas = new Canvas { Children = { follower } };
        overlay.Content = canvas;

        // Drive the live swatch per display frame (CompositionTarget.Rendering) — smoother than a
        // timer and far more reliable than MouseMove over a near-transparent overlay.
        EventHandler onFrame = (_, _) =>
        {
            Color c = SampleScreenColor();
            swatch.Fill = new SolidColorBrush(c);
            hexLabel.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            Point p = Mouse.GetPosition(canvas);
            Canvas.SetLeft(follower, p.X + 18);
            Canvas.SetTop(follower, p.Y + 18);
        };

        void Finish(bool commit)
        {
            CompositionTarget.Rendering -= onFrame;
            Color picked = SampleScreenColor();
            overlay.Close();
            if (commit)
            {
                (_h, _s, _v) = RgbToHsv(picked);
                Render(raise: true, updateHex: true);
            }
        }

        overlay.MouseLeftButtonDown += (_, _) => Finish(commit: true);
        overlay.KeyDown += (_, ev) => { if (ev.Key == Key.Escape) Finish(commit: false); };
        overlay.Loaded += (_, _) => { overlay.Activate(); overlay.Focus(); CompositionTarget.Rendering += onFrame; };
        overlay.Show();
    }

    private static Color SampleScreenColor()
    {
        GetCursorPos(out POINT p);
        IntPtr dc = GetDC(IntPtr.Zero);
        uint px = GetPixel(dc, p.X, p.Y);
        ReleaseDC(IntPtr.Zero, dc);
        return Color.FromRgb((byte)(px & 0xFF), (byte)((px >> 8) & 0xFF), (byte)((px >> 16) & 0xFF));
    }

    private void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHex)
            return;
        if (TryParse(HexInput.Text, out var c))
        {
            (_h, _s, _v) = RgbToHsv(c);
            Render(raise: true, updateHex: false);
        }
    }

    private static double Clamp01(double x) => Math.Clamp(x, 0, 1);

    private static bool TryParse(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
            return false;
        try { color = (Color)ColorConverter.ConvertFromString(hex); return true; }
        catch { return false; }
    }

    private static Color HsvToColor(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private static (double H, double S, double V) RgbToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        if (d != 0)
        {
            if (max == r) h = 60 * ((g - b) / d % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }
        if (h < 0) h += 360;
        double s = max == 0 ? 0 : d / max;
        return (h, s, max);
    }
}
