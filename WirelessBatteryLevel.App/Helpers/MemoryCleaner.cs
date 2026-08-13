using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WirelessBatteryLevel.App.Helpers
{
    public static class MemoryCleaner
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using var currentProcess = Process.GetCurrentProcess();
                    SetProcessWorkingSetSize(currentProcess.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryCleaner] Exception during TrimWorkingSet: {ex.Message}");
            }
        }
    }
}
