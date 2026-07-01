using System;
using Microsoft.Win32;

namespace LyricsOnTheGo.Services;

/// <summary>
/// "Start with Windows" via HKCU\Software\Microsoft\Windows\CurrentVersion\Run. This is OS
/// state (read/written directly), not part of the saved settings file.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LyricsOnTheGo";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enables/disables autostart. Returns true on success (caller reverts the UI on false).</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
                return false;

            if (enabled)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
