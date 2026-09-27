using System.Diagnostics;

namespace PetUsageOverlay;

internal static class DesktopApp
{
    public static Process? FindMainProcess()
    {
        var processes = Process.GetProcessesByName("ChatGPT");
        Process? main = null;
        foreach (var process in processes)
        {
            try
            {
                if (main is null && process.MainWindowHandle != IntPtr.Zero)
                {
                    main = process;
                    continue;
                }
            }
            catch (InvalidOperationException) { }
            process.Dispose();
        }
        return main;
    }
}
