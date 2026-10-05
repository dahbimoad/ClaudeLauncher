using System.IO;
using System.Text.Json;

namespace ClaudeLauncher;

public sealed class UsageRecord
{
    public int Count { get; set; }
    public DateTime LastUsed { get; set; }
}

/// <summary>Usage counts and pins, keyed by full folder path, persisted to %APPDATA%\ClaudeLauncher\state.json.</summary>
public sealed class LauncherState
{
    private static readonly string StateFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeLauncher", "state.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Dictionary<string, UsageRecord> Usage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Pinned { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CustomFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static LauncherState Load()
    {
        if (!File.Exists(StateFile))
            return new LauncherState();

        var loaded = JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(StateFile)) ?? new LauncherState();
        // Deserialization drops the case-insensitive comparers; restore them so paths match regardless of casing.
        loaded.Usage = new Dictionary<string, UsageRecord>(loaded.Usage, StringComparer.OrdinalIgnoreCase);
        loaded.Pinned = new HashSet<string>(loaded.Pinned, StringComparer.OrdinalIgnoreCase);
        loaded.CustomFolders = new HashSet<string>(loaded.CustomFolders, StringComparer.OrdinalIgnoreCase);
        return loaded;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void RecordLaunch(string folder)
    {
        if (!Usage.TryGetValue(folder, out var record))
        {
            record = new UsageRecord();
            Usage[folder] = record;
        }
        record.Count++;
        record.LastUsed = DateTime.Now;
    }

    public void RemoveCustomFolder(string folder)
    {
        CustomFolders.Remove(folder);
        Pinned.Remove(folder);
    }

    public void TogglePin(string folder)
    {
        if (!Pinned.Remove(folder))
            Pinned.Add(folder);
    }
}
