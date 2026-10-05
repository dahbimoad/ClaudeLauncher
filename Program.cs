using System.Diagnostics;
using System.Runtime.InteropServices;
using Velopack;

namespace ClaudeLauncher;

public static class Program
{
    private const int SwRestore = 9;

    [STAThread]
    public static void Main()
    {
        // Must run first: handles Velopack install/uninstall hooks (shortcuts) and exits during them.
        VelopackApp.Build().Run();

        using var singleInstance = new Mutex(true, "ClaudeLauncher.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            BringExistingInstanceToFront();
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void BringExistingInstanceToFront()
    {
        var current = Process.GetCurrentProcess();
        var existing = Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero);
        if (existing is null) return;

        ShowWindow(existing.MainWindowHandle, SwRestore);
        SetForegroundWindow(existing.MainWindowHandle);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
