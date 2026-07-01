using System;
using System.Threading;
using LyricsOnTheGo.Interop;

namespace LyricsOnTheGo.Glass;

/// <summary>
/// Owns the persistent-acrylic backdrop window on its OWN dedicated STA thread (with its
/// own system DispatcherQueue + message loop). Keeping the Composition off the WPF UI
/// thread avoids any dispatcher contention; the WPF window just sits transparently on top.
/// </summary>
public sealed class GlassWindow
{
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private GlassHost? _host;
    private Windows.System.DispatcherQueueController? _dispatcherController;

    public IntPtr Hwnd { get; private set; }

    public void Start(int x, int y, int width, int height)
    {
        _thread = new Thread(() => ThreadProc(x, y, width, height))
        {
            IsBackground = true,
            Name = "GlassComposition",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    private void ThreadProc(int x, int y, int width, int height)
    {
        _dispatcherController = Native.EnsureSystemDispatcherQueueOnCurrentThread();

        Hwnd = Native.CreateGlassWindow("LyricsOnTheGoGlass", x, y, width, height);
        _host = new GlassHost();
        _host.Initialize(Hwnd, width, height);
        Native.Show(Hwnd);

        _ready.Set();

        Native.RunMessageLoop();

        GC.KeepAlive(_host);
        GC.KeepAlive(_dispatcherController);
    }

    /// <summary>Updates the acrylic's rounded clip to the new size (marshalled to the glass thread).</summary>
    public void Resize(int width, int height)
    {
        var dq = _dispatcherController?.DispatcherQueue;
        if (dq is null || _host is null)
            return;

        dq.TryEnqueue(() => _host.Resize(width, height));
    }

    /// <summary>Live-updates the tint colour + opacity (marshalled to the glass thread).</summary>
    public void UpdateTint(byte r, byte g, byte b, float opacity)
    {
        var dq = _dispatcherController?.DispatcherQueue;
        if (dq is null || _host is null)
            return;

        dq.TryEnqueue(() => _host.UpdateTint(r, g, b, opacity));
    }

    /// <summary>Shows/hides the glass backdrop together with the UI (marshalled to the glass thread).</summary>
    public void SetVisible(bool visible)
    {
        var dq = _dispatcherController?.DispatcherQueue;
        if (dq is null)
        {
            Native.SetVisible(Hwnd, visible);
            return;
        }
        dq.TryEnqueue(() => Native.SetVisible(Hwnd, visible));
    }

    public void Close()
    {
        if (Hwnd != IntPtr.Zero)
            Native.PostClose(Hwnd);
    }
}
