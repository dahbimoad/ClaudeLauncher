using System.Net.Http;
using Velopack;
using Velopack.Sources;

namespace ClaudeLauncher;

/// <summary>Pulls new versions from the GitHub releases of this repo and applies them on restart or exit.</summary>
public sealed class AppUpdater
{
    private const string RepoUrl = "https://github.com/dahbimoad/ClaudeLauncher";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));
    private VelopackAsset? _downloaded;

    /// <summary>
    /// Downloads the newest release in the background. Returns its version, or null when there is
    /// nothing to install: already up to date, running from a dev build, or offline.
    /// </summary>
    public async Task<string?> DownloadLatestAsync()
    {
        if (!_manager.IsInstalled)
            return null;

        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
                return null;

            await _manager.DownloadUpdatesAsync(update);
            _downloaded = update.TargetFullRelease;
            return _downloaded.Version.ToString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // No network or GitHub unreachable: the launcher works fine without updating, so try again next launch.
            return null;
        }
    }

    public void RestartIntoUpdate()
    {
        if (_downloaded is not null)
            _manager.ApplyUpdatesAndRestart(_downloaded);
    }

    /// <summary>Installs a downloaded update silently once the app has closed, so the next launch is current.</summary>
    public void ApplyAfterExit()
    {
        if (_downloaded is not null)
            _manager.WaitExitThenApplyUpdates(_downloaded, silent: true, restart: false);
    }
}
