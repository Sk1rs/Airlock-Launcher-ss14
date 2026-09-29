using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

/// <summary>
/// The facts someone helping with a problem always has to ask for.
/// </summary>
/// <remarks>
/// Deliberately plain text and short enough to paste into a chat. It reports only what the
/// launcher itself knows about this machine's setup: no account names, tokens or server
/// addresses, since these reports end up in public channels.
/// </remarks>
public static class DiagnosticsReport
{
    public static string Build()
    {
        var text = new StringBuilder();

        text.AppendLine("Airlock Launcher diagnostics");
        text.AppendLine($"Version: {LauncherVersion.Version}");
        text.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"Culture: {CultureInfo.CurrentUICulture.Name}");
        text.AppendLine();

        text.AppendLine($"Data root: {LauncherPaths.DirDataRoot}");
        text.AppendLine($"Install: {LauncherPaths.DirLauncherInstall}");
        text.AppendLine();

        Report(text, "Server content", LauncherPaths.DirServerContent);
        Report(text, "Engines", LauncherPaths.DirEngineInstallations);
        Report(text, "Modules", LauncherPaths.DirModuleInstallations);
        Report(text, "Logs", LauncherPaths.DirLogs);
        text.AppendLine();

        text.AppendLine($"Free on data drive: {FreeSpace(LauncherPaths.DirDataRoot)}");

        return text.ToString();
    }

    /// <summary>
    /// The download folders and their sizes on one line, for the options panel.
    /// </summary>
    public static string StorageSummary()
    {
        var content = Measure(LauncherPaths.DirServerContent);
        var engines = Measure(LauncherPaths.DirEngineInstallations);
        var modules = Measure(LauncherPaths.DirModuleInstallations);
        var total = content.Bytes + engines.Bytes + modules.Bytes;

        return $"{Format(total)} — content {Format(content.Bytes)}, engines {Format(engines.Bytes)}, " +
               $"modules {Format(modules.Bytes)}; free {FreeSpace(LauncherPaths.DirDataRoot)}";
    }

    private static void Report(StringBuilder text, string label, string path)
    {
        if (!Directory.Exists(path))
        {
            text.AppendLine($"{label}: missing");
            return;
        }

        var (bytes, files) = Measure(path);

        text.AppendLine($"{label}: {Format(bytes)} in {files} file(s)");
    }

    /// <summary>
    /// Total size of a folder tree. Files that vanish or refuse to be read mid-walk are skipped:
    /// a diagnostics report must never be the thing that throws.
    /// </summary>
    private static (long Bytes, int Files) Measure(string path)
    {
        long bytes = 0;
        var files = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                    files++;
                }
                catch (Exception)
                {
                    // Gone or locked; not worth reporting.
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not measure {Path}", path);
        }

        return (bytes, files);
    }

    private static string FreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));

            return root == null
                ? "unknown"
                : Format(new DriveInfo(root).AvailableFreeSpace);
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private static string Format(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.0} GB",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
