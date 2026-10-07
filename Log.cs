using System.IO;

namespace ClaudeLauncher;

/// <summary>Appends timestamped lines to %APPDATA%\ClaudeLauncher\launcher.log so a failed launch can be diagnosed.</summary>
public static class Log
{
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeLauncher", "launcher.log");

    // Keeps the file small: once it passes this size the next write starts it over.
    private const long MaxBytes = 512 * 1024;

    private static readonly Lock WriteLock = new();

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception exception) => Write("ERROR", $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        lock (WriteLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                File.Delete(FilePath);
            File.AppendAllText(FilePath, line);
        }
    }
}
