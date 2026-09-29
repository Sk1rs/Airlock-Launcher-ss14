using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Reconstructs past play sessions from another launcher's log files.
/// </summary>
/// <remarks>
/// No launcher records playtime, but they all log enough to work it out after the fact: the command
/// used to start the game (which carries the server address), the moment the client process was
/// started, and the moment its output pipes hit EOF, which is when the client exited.
/// Only reaches as far back as the log files that are still around, of course.
/// </remarks>
public static partial class LauncherLogSessions
{
    /// <summary>
    /// A play session recovered from the logs.
    /// </summary>
    public sealed record Session(string Address, DateTimeOffset Start, TimeSpan Duration);

    // "2026-08-26 00:44:22.707 +10:00 [DBG] ..."
    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2}) \[")]
    private static partial Regex TimestampRegex { get; }

    // Arguments are logged as "[12] --ss14-address [13] ss14://example.com/".
    [GeneratedRegex(@"--ss14-address\s+\[\d+\]\s+(\S+)")]
    private static partial Regex AddressRegex { get; }

    [GeneratedRegex(@"logging for new client with PID (\d+)")]
    private static partial Regex ClientStartedRegex { get; }

    [GeneratedRegex(@"ending pipe logging for (\d+)")]
    private static partial Regex ClientExitedRegex { get; }

    /// <summary>
    /// Reads every <c>launcher-*.log</c> in the folder as one stream, oldest first.
    /// </summary>
    /// <remarks>
    /// Deliberately not per file: logs roll over at midnight, so a session started before midnight
    /// ends in the next day's file.
    /// </remarks>
    public static List<Session> ReadFolder(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
            return [];

        try
        {
            // The file names carry the date, so sorting by name sorts by time.
            var lines = Directory
                .EnumerateFiles(logDirectory, "launcher-*.log")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe);

            return Parse(lines);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read play sessions from {Directory}", logDirectory);

            return [];
        }
    }

    private static IEnumerable<string> ReadLinesSafe(string path)
    {
        // The other launcher may still be running and holding the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    /// <summary>
    /// Pulls sessions out of launcher log lines.
    /// </summary>
    public static List<Session> Parse(IEnumerable<string> lines)
    {
        var sessions = new List<Session>();

        // Set by the launch command line, claimed by the client-started line right after it.
        string? pendingAddress = null;
        var running = new Dictionary<int, (string Address, DateTimeOffset Start)>();

        foreach (var line in lines)
        {
            if (AddressRegex.Match(line) is { Success: true } address)
            {
                pendingAddress = address.Groups[1].Value;
                continue;
            }

            if (ClientStartedRegex.Match(line) is { Success: true } started)
            {
                if (pendingAddress == null || ParseTimestamp(line) is not { } startTime)
                    continue;

                // A redial reuses nothing: each client gets its own PID and its own line.
                running[int.Parse(started.Groups[1].Value)] = (pendingAddress, startTime);
                pendingAddress = null;
                continue;
            }

            if (ClientExitedRegex.Match(line) is { Success: true } exited)
            {
                var pid = int.Parse(exited.Groups[1].Value);
                if (!running.Remove(pid, out var session) || ParseTimestamp(line) is not { } endTime)
                    continue;

                // Both output pipes log an EOF; the second one finds nothing left to close.
                var duration = endTime - session.Start;
                if (duration > TimeSpan.Zero)
                    sessions.Add(new Session(session.Address, session.Start, duration));
            }
        }

        // Sessions still open never got an exit logged: the launcher was closed mid-game, or the
        // logs simply end there. Nothing sensible to count for them.
        return sessions;
    }

    private static DateTimeOffset? ParseTimestamp(string line)
    {
        if (TimestampRegex.Match(line) is not { Success: true } match)
            return null;

        if (!DateTimeOffset.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var timestamp))
        {
            return null;
        }

        return timestamp;
    }
}
