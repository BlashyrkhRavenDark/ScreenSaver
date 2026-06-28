using System;
using System.IO;
using Microsoft.Win32;

namespace ScreenSaver.Tests
{
    /// <summary>
    /// A throwaway HKCU subkey under SOFTWARE\Demo_ScreenSaver_Tests, unique per
    /// instance, deleted on dispose. The harness points the injectable RegistryPath
    /// here so the registry round-trip never reads or writes the operator's live
    /// SOFTWARE\Demo_ScreenSaver key (no snapshot/restore of real settings).
    /// </summary>
    internal sealed class TempRegistryKey : IDisposable
    {
        public string Path { get; }

        public TempRegistryKey()
        {
            Path = "SOFTWARE\\Demo_ScreenSaver_Tests\\" + Guid.NewGuid().ToString("N");
        }

        public void Dispose()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(Path, throwOnMissingSubKey: false); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// A unique throwaway directory under the OS temp path, deleted on dispose.
    /// Used as the CoverBlocklist cacheDir so its blocklist.txt never lands in the
    /// operator's real %USERPROFILE%\ScreenSaverCovers.
    /// </summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ScreenSaverTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }
}
