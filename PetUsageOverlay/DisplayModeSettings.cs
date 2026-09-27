using System.IO;
using System.Text.Json;

namespace PetUsageOverlay;

internal enum DisplayMode { Orbit, Below }

internal static class DisplayModeSettings
{
    public static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PetUsageOverlay", "settings.json");

    public static DisplayMode Load()
    {
        try
        {
            using var file = File.OpenRead(Path);
            using var document = JsonDocument.Parse(file);
            var value = document.RootElement.GetProperty("displayMode").GetString();
            return value == "below" ? DisplayMode.Below : DisplayMode.Orbit;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return DisplayMode.Orbit;
        }
    }

    public static void Save(DisplayMode mode)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var json = JsonSerializer.Serialize(new { displayMode = mode == DisplayMode.Below ? "below" : "orbit" });
        File.WriteAllText(Path, json);
    }
}
