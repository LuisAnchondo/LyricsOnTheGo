using System;
using System.Numerics;
using LyricsOnTheGo.Interop;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;

namespace LyricsOnTheGo.Glass;

/// <summary>
/// Builds the persistent acrylic visual tree for the glass window:
/// HostBackdrop (system-blurred desktop behind the window) + a colour tint, clipped
/// to rounded corners. Because the app owns this brush, Windows does NOT auto-fade it
/// when the window goes inactive — the whole reason for this native rewrite.
/// </summary>
public sealed class GlassHost
{
    private Compositor _compositor = null!;
    private DesktopWindowTarget _target = null!;
    private CompositionRoundedRectangleGeometry _clipGeometry = null!;
    private SpriteVisual _tint = null!;
    private CompositionColorBrush _tintBrush = null!;

    public void Initialize(IntPtr hwnd, int width, int height)
    {
        _compositor = new Compositor();
        _target = CompositionInterop.CreateDesktopWindowTarget(_compositor, hwnd, isTopmost: true);

        var root = _compositor.CreateContainerVisual();
        root.RelativeSizeAdjustment = new Vector2(1f, 1f);
        _target.Root = root;

        // Rounded-corner clip — composition-level, so it works on Windows 10 AND 11
        _clipGeometry = _compositor.CreateRoundedRectangleGeometry();
        _clipGeometry.CornerRadius = new Vector2(12f, 12f);
        _clipGeometry.Size = new Vector2(width, height);
        root.Clip = _compositor.CreateGeometricClip(_clipGeometry);

        // Acrylic backdrop: the desktop behind the window, system-blurred. Owned by us.
        var hostBackdrop = _compositor.CreateHostBackdropBrush();
        var backdrop = _compositor.CreateSpriteVisual();
        backdrop.RelativeSizeAdjustment = new Vector2(1f, 1f);
        backdrop.Brush = hostBackdrop;
        root.Children.InsertAtBottom(backdrop);

        // Tint layer (user's bgcolor @ bgopacity). Updated live from settings.
        _tintBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 0x08, 0x08, 0x08));
        _tint = _compositor.CreateSpriteVisual();
        _tint.RelativeSizeAdjustment = new Vector2(1f, 1f);
        _tint.Brush = _tintBrush;
        _tint.Opacity = 0.35f;
        root.Children.InsertAtTop(_tint);
    }

    public void Resize(int width, int height)
    {
        if (_clipGeometry != null)
            _clipGeometry.Size = new Vector2(width, height);
    }

    /// <summary>Live-updates the tint colour (RGB) and opacity (0–1). Must run on the glass thread.</summary>
    public void UpdateTint(byte r, byte g, byte b, float opacity)
    {
        if (_tintBrush is null || _tint is null)
            return;
        _tintBrush.Color = Color.FromArgb(255, r, g, b);
        _tint.Opacity = Math.Clamp(opacity, 0f, 1f);
    }
}
