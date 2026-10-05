using System.IO;

namespace ClaudeLauncher;

public sealed record ProjectEntry(
    string Name, string FolderPath, bool IsPinned, bool IsCustom, int UseCount, DateTime? LastUsed)
{
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string DisplayPath => FolderPath.StartsWith(UserProfile, StringComparison.OrdinalIgnoreCase)
        ? "~" + FolderPath[UserProfile.Length..]
        : FolderPath;

    public string Meta => LastUsed is { } last ? $"{UseCount}× · {Ago(last)}" : "";

    /// <summary>
    /// Subfolders of <paramref name="root"/> plus the user's added folders that still exist:
    /// pinned first, then most used, then most recent, then by name.
    /// </summary>
    public static List<ProjectEntry> LoadAll(string root, LauncherState state)
    {
        var rootFolders = new DirectoryInfo(root).EnumerateDirectories()
            .Where(dir => !dir.Attributes.HasFlag(FileAttributes.Hidden));
        var customFolders = state.CustomFolders
            .Where(Directory.Exists)
            .Select(path => new DirectoryInfo(path));

        return rootFolders.Concat(customFolders)
            .DistinctBy(dir => dir.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(dir => FromFolder(dir, state))
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.UseCount)
            .ThenByDescending(p => p.LastUsed)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProjectEntry FromFolder(DirectoryInfo dir, LauncherState state)
    {
        state.Usage.TryGetValue(dir.FullName, out var usage);
        return new ProjectEntry(dir.Name, dir.FullName,
            state.Pinned.Contains(dir.FullName), state.CustomFolders.Contains(dir.FullName),
            usage?.Count ?? 0, usage?.LastUsed);
    }

    private static string Ago(DateTime when)
    {
        var elapsed = DateTime.Now - when;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }
}
