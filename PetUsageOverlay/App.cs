using System.Windows;

namespace PetUsageOverlay;

public static class App
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--diagnose"))
        {
            Diagnostics.Run().GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--check-layout"))
        {
            PlacementChecks.Run();
            return;
        }
        if (args.Length == 2 && args[0] == "--set-mode")
        {
            var mode = args[1] switch
            {
                "orbit" => DisplayMode.Orbit,
                "below" => DisplayMode.Below,
                _ => throw new ArgumentException("模式只能是 orbit 或 below。")
            };
            DisplayModeSettings.Save(mode);
            Console.WriteLine($"displayMode={args[1]}");
            return;
        }
        if (args.Contains("--probe-progress"))
        {
            var probe = ProgressDetector.Probe();
            Console.WriteLine($"windows={probe.Windows} textElements={probe.TextElements} statusTexts={probe.StatusTexts} maxRight={probe.MaxRight:0} maxBottom={probe.MaxBottom:0}");
            return;
        }
        if (args.Length == 2 && (args[0] == "--render-preview" ||
            args[0] == "--render-preview-below" || args[0] == "--render-preview-controls"))
        {
            _ = new System.Windows.Application();
            var previewWindow = new UsageWindow();
            previewWindow.WritePreview(args[1],
                args[0] == "--render-preview-below" ? DisplayMode.Below : DisplayMode.Orbit,
                args[0] == "--render-preview-controls");
            previewWindow.Close();
            return;
        }

        using var instance = new Mutex(initiallyOwned: true, name: "Local\\CodexPetUsageOverlay", out var isFirstInstance);
        if (!isFirstInstance) return;

        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new UsageWindow();
        app.Run(window);
    }
}
