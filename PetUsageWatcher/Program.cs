using System.Diagnostics;

namespace PetUsageWatcher;

internal static class Program
{
    private static void Main()
    {
        using var instance = new Mutex(initiallyOwned: true,
            name: "Local\\CodexPetUsageAppWatcher", out var firstInstance);
        if (!firstInstance) return;

        using var appClosed = new EventWaitHandle(initialState: false,
            mode: EventResetMode.AutoReset, name: "Local\\CodexPetUsageAppClosed");
        var overlay = Path.Combine(AppContext.BaseDirectory, "PetUsageOverlay.exe");
        (int Id, IntPtr Window)? previousMainWindow = null;
        while (true)
        {
            var mainWindow = FindMainWindow();
            if (mainWindow is not null && mainWindow != previousMainWindow && File.Exists(overlay))
            {
                try
                {
                    using var launched = Process.Start(new ProcessStartInfo(overlay)
                    {
                        UseShellExecute = true
                    });
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Try again after the next ChatGPT launch.
                }
            }
            previousMainWindow = mainWindow;
            if (appClosed.WaitOne(2000)) previousMainWindow = null;
        }
    }

    private static (int Id, IntPtr Window)? FindMainWindow()
    {
        var processes = Process.GetProcessesByName("ChatGPT");
        (int Id, IntPtr Window)? mainWindow = null;
        foreach (var process in processes)
        {
            try
            {
                var window = process.MainWindowHandle;
                if (mainWindow is null && window != IntPtr.Zero)
                    mainWindow = (process.Id, window);
            }
            catch (InvalidOperationException) { }
            process.Dispose();
        }
        return mainWindow;
    }
}
