using System.Diagnostics;

namespace ClaudeLauncher;

/// <summary>Starts Claude Code in a Windows Terminal tab, by default inside one shared named window.</summary>
public static class TerminalLauncher
{
    // wt routes every "-w <name>" call to the same window, creating it on first use.
    private const string SharedWindowName = "claude-launcher";

    public static void OpenInSharedWindow(ProjectEntry project) => Start(project, SharedWindowName);

    public static void OpenInNewWindow(ProjectEntry project) => Start(project, "new");

    private static void Start(ProjectEntry project, string windowTarget)
    {
        var startInfo = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var arg in new[]
                 {
                     "-w", windowTarget,
                     "new-tab",
                     "--title", project.Name,
                     "--suppressApplicationTitle",
                     "-d", project.FolderPath,
                     "pwsh", "-NoExit", "-Command", "claude",
                 })
        {
            startInfo.ArgumentList.Add(arg);
        }
        Process.Start(startInfo);
    }
}
