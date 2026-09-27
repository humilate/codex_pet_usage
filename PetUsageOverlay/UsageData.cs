using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PetUsageOverlay;

internal record Quota(double? RemainingPercent, DateTimeOffset? ResetsAt);

internal static class JsonRead
{
    public static JsonElement? Get(JsonElement value, params string[] path)
    {
        foreach (var key in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value)) return null;
        }
        return value;
    }

    public static double? Number(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;
    public static long? Long(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v ? v.GetInt64() : null;
}

internal static class CodexPaths
{
    public static readonly string Home = Environment.GetEnvironmentVariable("CODEX_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

    public static string? FindCli()
    {
        foreach (var process in Process.GetProcessesByName("codex"))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is not null && File.Exists(path)) return path;
            }
            catch { /* Another Codex process may not allow inspection. */ }
            finally { process.Dispose(); }
        }

        var bin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (!Directory.Exists(bin)) return null;
        return Directory.EnumerateFiles(bin, "codex.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
}

internal static class QuotaReader
{
    public static async Task<(Quota FiveHour, Quota Weekly)?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var cli = CodexPaths.FindCli();
        if (cli is null) return null;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(cli, "app-server --stdio")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
            }
        };

        try
        {
            process.Start();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(18));
            await Send(process, new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "pet-usage-overlay", version = "0.1.0" }, capabilities = new { } } });

            var sentRead = false;
            while (!timeout.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null) break;
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var id = JsonRead.Long(JsonRead.Get(root, "id"));
                if (id == 1 && !sentRead)
                {
                    await Send(process, new { method = "initialized" });
                    await Send(process, new { id = 2, method = "account/rateLimits/read", @params = new { } });
                    sentRead = true;
                }
                else if (id == 2)
                {
                    var limits = JsonRead.Get(root, "result", "rateLimits");
                    if (limits is null) return null;
                    return (ReadQuota(limits.Value, "primary"), ReadQuota(limits.Value, "secondary"));
                }
            }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        return null;
    }

    private static async Task Send(Process process, object message)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await process.StandardInput.FlushAsync();
    }

    private static Quota ReadQuota(JsonElement limits, string name)
    {
        var bucket = JsonRead.Get(limits, name);
        var used = JsonRead.Number(bucket is null ? null : JsonRead.Get(bucket.Value, "usedPercent"));
        var reset = JsonRead.Long(bucket is null ? null : JsonRead.Get(bucket.Value, "resetsAt"));
        return new Quota(used is null ? null : Math.Clamp(100 - used.Value, 0, 100), reset is null ? null : DateTimeOffset.FromUnixTimeSeconds(reset.Value));
    }
}

internal static class Diagnostics
{
    public static async Task Run()
    {
        var quota = await QuotaReader.ReadAsync();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            cliFound = CodexPaths.FindCli() is not null,
            fiveHourRemaining = quota?.FiveHour.RemainingPercent,
            weeklyRemaining = quota?.Weekly.RemainingPercent
        }));
    }
}
