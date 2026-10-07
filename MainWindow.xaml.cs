using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ClaudeLauncher;

public partial class MainWindow : Window
{
    private static readonly string ProjectsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "projects");

    private readonly LauncherState _state = LauncherState.Load();
    private readonly AppUpdater _updater = new();
    private List<ProjectEntry> _allProjects = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (await _updater.DownloadLatestAsync() is not { } newVersion) return;
        UpdateText.Text = $"Version {newVersion} is ready.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void OnRestartToUpdateClick(object sender, RoutedEventArgs e) => _updater.RestartIntoUpdate();

    private void OnClosed(object? sender, EventArgs e) => _updater.ApplyAfterExit();

    // Windows 11 draws a light title bar unless the window opts into immersive dark mode.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        const int DwmwaUseImmersiveDarkMode = 20;
        var enabled = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private ProjectEntry? SelectedProject => ProjectList.SelectedItem as ProjectEntry;

    // Re-reading the folder on every activation picks up projects created while the launcher was open.
    private void OnActivated(object? sender, EventArgs e)
    {
        Reload(SelectedProject?.FolderPath);
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Reload(string? keepSelectedPath)
    {
        _allProjects = ProjectEntry.LoadAll(ProjectsRoot, _state);
        ApplyFilter(keepSelectedPath);
    }

    private void ApplyFilter(string? keepSelectedPath = null)
    {
        var query = SearchBox.Text.Trim();
        var visible = _allProjects
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ProjectList.ItemsSource = visible;
        ProjectList.SelectedItem = visible.FirstOrDefault(p => p.FolderPath == keepSelectedPath)
                                   ?? visible.FirstOrDefault();
        if (ProjectList.SelectedItem is not null)
            ProjectList.ScrollIntoView(ProjectList.SelectedItem);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.Down: MoveSelection(+1); break;
            case Key.Up: MoveSelection(-1); break;
            case Key.Enter when ctrl: Launch(SelectedProject, TerminalLauncher.OpenInNewWindow); break;
            case Key.Enter: Launch(SelectedProject, TerminalLauncher.OpenInCurrentWindow); break;
            case Key.P when ctrl: TogglePin(SelectedProject); break;
            case Key.Escape: SearchBox.Clear(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void MoveSelection(int step)
    {
        var count = ProjectList.Items.Count;
        if (count == 0) return;
        ProjectList.SelectedIndex = Math.Clamp(ProjectList.SelectedIndex + step, 0, count - 1);
        ProjectList.ScrollIntoView(ProjectList.SelectedItem);
    }

    private ProjectEntry? RowProject(object sender) => (sender as FrameworkElement)?.DataContext as ProjectEntry;

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) =>
        Launch(SelectedProject, TerminalLauncher.OpenInCurrentWindow);

    private void OnOpenClick(object sender, RoutedEventArgs e) =>
        Launch(SelectedProject, TerminalLauncher.OpenInCurrentWindow);

    private void OnNewWindowClick(object sender, RoutedEventArgs e) =>
        Launch(SelectedProject, TerminalLauncher.OpenInNewWindow);

    private void OnPinClick(object sender, RoutedEventArgs e) => TogglePin(RowProject(sender));

    private void OnMenuOpenClick(object sender, RoutedEventArgs e) =>
        Launch(RowProject(sender), TerminalLauncher.OpenInCurrentWindow);

    private void OnMenuNewWindowClick(object sender, RoutedEventArgs e) =>
        Launch(RowProject(sender), TerminalLauncher.OpenInNewWindow);

    private void OnMenuPinClick(object sender, RoutedEventArgs e) => TogglePin(RowProject(sender));

    private void OnMenuExplorerClick(object sender, RoutedEventArgs e)
    {
        if (RowProject(sender) is not { } project) return;
        var explorer = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        explorer.ArgumentList.Add(project.FolderPath);
        Process.Start(explorer);
    }

    private void OnMenuRemoveClick(object sender, RoutedEventArgs e)
    {
        if (RowProject(sender) is not { } project) return;
        _state.RemoveCustomFolder(project.FolderPath);
        _state.Save();
        Reload(null);
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Add folders to Claude Launcher", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;

        foreach (var folder in dialog.FolderNames)
            _state.CustomFolders.Add(folder);
        _state.Save();

        SearchBox.Clear();
        Reload(dialog.FolderNames[0]);
    }

    private void TogglePin(ProjectEntry? project)
    {
        if (project is null) return;
        _state.TogglePin(project.FolderPath);
        _state.Save();
        Reload(project.FolderPath);
    }

    private void Launch(ProjectEntry? project, Action<ProjectEntry> openTerminal)
    {
        if (project is null) return;

        try
        {
            openTerminal(project);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            // Stays open so the user can retry; the log names the missing program.
            Log.Error($"Could not open a terminal for '{project.Name}' ({project.FolderPath})", ex);
            return;
        }
        _state.RecordLaunch(project.FolderPath);
        _state.Save();

        SearchBox.Clear();
        WindowState = WindowState.Minimized;
    }
}
