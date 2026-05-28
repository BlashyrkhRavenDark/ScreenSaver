using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ScreenSaver.Launcher
{
    /// <summary>
    /// Tiny native (AOT-compiled) screensaver launcher. Lives at
    /// C:\Windows\System32\ScreenSaver.scr so the Windows screensaver picker
    /// can enumerate it. On launch, looks up the real screensaver exe at
    /// HKCU\SOFTWARE\Demo_ScreenSaver\ScreenSaverPath and runs it with the
    /// same args Windows passed us (typically /s, /p:hwnd, or /c).
    ///
    /// Why a launcher: Windows enumerates *.scr only in System32, so we need
    /// SOMETHING there to appear in the picker. The real screensaver is
    /// framework-dependent and lives in %LOCALAPPDATA% to avoid AV
    /// false-positives on self-contained single-file binaries in System32.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string targetExe = ResolveTargetExe();
                if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
                {
                    // Nothing to launch and nowhere to log. Silently exit so the
                    // screensaver picker doesn't get a popup mid-preview.
                    return 1;
                }

                var psi = new ProcessStartInfo(targetExe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(targetExe) ?? string.Empty
                };
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
            }
            catch
            {
                // Swallow - a crash dialog from a screensaver is the wrong UX.
            }
            return 0;
        }

        private static string ResolveTargetExe()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Demo_ScreenSaver"))
                {
                    return key?.GetValue("ScreenSaverPath") as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
