using System.Diagnostics;
using System.IO;

namespace ClaudeLauncher;

/// <summary>Starts Claude Code in a Windows Terminal tab, by default inside the terminal window already open.</summary>
public static class TerminalLauncher
{
    // "-w 0" is wt's "most recently used window": the tab lands in the terminal already open, or a new window if none is.
    private const string CurrentWindow = "0";

    // PowerShell 7 when installed, otherwise the Windows PowerShell that ships with every Windows 10/11.
    private static readonly string[] ShellCandidates = ["pwsh.exe", "powershell.exe"];

    public static void OpenInCurrentWindow(ProjectEntry project) => Start(project, CurrentWindow);

    public static void OpenInNewWindow(ProjectEntry project) => Start(project, "new");

    private static void Start(ProjectEntry project, string windowTarget)
    {
        var shell = ResolveShell();
        var startInfo = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var arg in new[]
                 {
                     "-w", windowTarget,
                     "new-tab",
                     "--title", project.Name,
                     "--suppressApplicationTitle",
                     "-d", project.FolderPath,
                     shell, "-NoExit", "-Command", "claude",
                 })
        {
            startInfo.ArgumentList.Add(arg);
        }
        RemoveInheritedClaudeSessionMarkers(startInfo);

        Log.Info($"Launching '{project.Name}': wt.exe {string.Join(' ', startInfo.ArgumentList.Select(Quote))}");
        using var process = Process.Start(startInfo);
        Log.Info($"wt.exe started (pid {process?.Id.ToString() ?? "none"})");
    }

    // When the launcher itself was started from inside a Claude Code session, it inherits that session's markers
    // (CLAUDECODE, CLAUDE_CODE_CHILD_SESSION, ...). Passed on to the new tab, they make Claude treat it as a child
    // session and turn transcript saving off. Variables the user set on purpose (User/Machine scope) are kept.
    private static void RemoveInheritedClaudeSessionMarkers(ProcessStartInfo startInfo)
    {
        var markers = startInfo.Environment.Keys
            .Where(name => name.Equals("CLAUDECODE", StringComparison.OrdinalIgnoreCase)
                           || name.Equals("CLAUDE_PID", StringComparison.OrdinalIgnoreCase)
                           || name.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase))
            .Where(name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) is null
                           && Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) is null)
            .ToList();

        foreach (var name in markers)
        {
            startInfo.Environment.Remove(name);
        }
        if (markers.Count > 0)
        {
            Log.Info($"Removed inherited Claude session variables: {string.Join(", ", markers)}");
        }
    }

    private static string ResolveShell()
    {
        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var shell in ShellCandidates)
        {
            var found = pathDirectories.Select(dir => Path.Combine(dir, shell)).FirstOrDefault(File.Exists);
            if (found is not null)
            {
                Log.Info($"Shell resolved: {found}");
                return found;
            }
            Log.Info($"Shell not on PATH: {shell}");
        }

        throw new FileNotFoundException($"Neither {string.Join(" nor ", ShellCandidates)} was found on PATH.");
    }

    private static string Quote(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
