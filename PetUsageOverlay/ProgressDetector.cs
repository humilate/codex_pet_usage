using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace PetUsageOverlay;

internal static class ProgressDetector
{
    private static readonly string[] StatusWords =
    [
        "正在", "准备中", "处理中", "生成中", "绘制中",
        "Preparing", "Generating", "Working", "Running"
    ];

    public static bool OverlapsCards(IReadOnlyList<Rect> cardsOnScreen)
    {
        if (cardsOnScreen.Count == 0) return false;
        var searchArea = cardsOnScreen[0];
        for (var index = 1; index < cardsOnScreen.Count; index++)
            searchArea.Union(cardsOnScreen[index]);
        searchArea.Inflate(8, 8);
        foreach (var handle in CodexWindows())
        {
            try
            {
                var window = AutomationElement.FromHandle(handle);
                if (window is null || !window.Current.BoundingRectangle.IntersectsWith(searchArea)) continue;

                var condition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Text);
                var texts = window.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement element in texts)
                {
                    try
                    {
                        var name = element.Current.Name;
                        var bounds = element.Current.BoundingRectangle;
                        if (cardsOnScreen.Any(card => MatchesToastText(name, bounds, card)))
                            return true;
                    }
                    catch (ElementNotAvailableException) { /* Toast changed during the scan. */ }
                }
            }
            catch (Exception)
            {
                // The Codex window may be replacing its accessibility tree.
            }
        }
        return false;
    }

    public static (int Windows, int TextElements, int StatusTexts, double MaxRight, double MaxBottom) Probe()
    {
        var windows = 0;
        var textsFound = 0;
        var statusFound = 0;
        var maxRight = 0.0;
        var maxBottom = 0.0;
        foreach (var handle in CodexWindows())
        {
            try
            {
                windows++;
                var window = AutomationElement.FromHandle(handle);
                var texts = window.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                textsFound += texts.Count;
                foreach (AutomationElement element in texts)
                {
                    var name = element.Current.Name;
                    var bounds = element.Current.BoundingRectangle;
                    if (!bounds.IsEmpty)
                    {
                        maxRight = Math.Max(maxRight, bounds.Right);
                        maxBottom = Math.Max(maxBottom, bounds.Bottom);
                    }
                    if (LooksLikeProgress(name))
                        statusFound++;
                }
            }
            catch { /* Diagnostic only. */ }
        }
        return (windows, textsFound, statusFound, maxRight, maxBottom);
    }

    private static List<IntPtr> CodexWindows()
    {
        var processes = Process.GetProcessesByName("ChatGPT");
        var ids = processes.Select(process => process.Id).ToHashSet();
        foreach (var process in processes) process.Dispose();
        var windows = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (IsWindowVisible(hwnd) && ids.Contains((int)pid)) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    internal static bool LooksLikeProgress(string name) =>
        name.Length is >= 2 and <= 100 &&
        StatusWords.Any(word => name.TrimStart().StartsWith(word, StringComparison.OrdinalIgnoreCase));

    internal static bool MatchesToastText(string name, Rect bounds, Rect card)
    {
        if (!LooksLikeProgress(name) || bounds.IsEmpty || bounds.Height > 50 ||
            bounds.Top >= card.Top + 50 || bounds.Bottom <= card.Top - 10) return false;
        var expanded = card;
        expanded.Inflate(8, 8);
        return bounds.IntersectsWith(expanded);
    }

    private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hwnd);
}
