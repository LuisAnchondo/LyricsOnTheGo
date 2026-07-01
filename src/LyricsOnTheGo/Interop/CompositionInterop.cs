using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace LyricsOnTheGo.Interop;

/// <summary>
/// Bridges a Win32 HWND to a <see cref="Windows.UI.Composition"/> compositor by
/// creating a <see cref="DesktopWindowTarget"/> for the window — the foundation of
/// app-owned acrylic that does NOT fade when the window is inactive.
/// </summary>
[ComImport]
[Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICompositorDesktopInterop
{
    void CreateDesktopWindowTarget(IntPtr hwndTarget, [MarshalAs(UnmanagedType.U1)] bool isTopmost, out IntPtr result);
    void EnsureOnThreadForDesktopWindowTarget(IntPtr hwnd);
}

internal static class CompositionInterop
{
    public static DesktopWindowTarget CreateDesktopWindowTarget(Compositor compositor, IntPtr hwnd, bool isTopmost)
    {
        IntPtr compositorAbi = MarshalInspectable<Compositor>.FromManaged(compositor);
        try
        {
            Guid iid = typeof(ICompositorDesktopInterop).GUID;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(compositorAbi, ref iid, out IntPtr interopPtr));
            try
            {
                var interop = (ICompositorDesktopInterop)Marshal.GetObjectForIUnknown(interopPtr);
                interop.CreateDesktopWindowTarget(hwnd, isTopmost, out IntPtr rawTarget);
                // FromAbi takes its own reference; the raw pointer leaks one ref on a
                // window-lifetime object, which is acceptable.
                return MarshalInspectable<DesktopWindowTarget>.FromAbi(rawTarget);
            }
            finally
            {
                Marshal.Release(interopPtr);
            }
        }
        finally
        {
            Marshal.Release(compositorAbi);
        }
    }
}
