# Builds Releases\ClaudeLauncher-win-Setup.exe (installs to %LocalAppData%, adds Desktop + Start Menu shortcuts).
# Bump <Version> in ClaudeLauncher.csproj before re-running; installing a newer Setup.exe upgrades in place.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$project = Get-Content ClaudeLauncher.csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Remove-Item publish -Recurse -Force -ErrorAction SilentlyContinue
# Self-contained: the .NET runtime ships inside the app, so nothing else gets installed on the user's PC.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

vpk pack `
    --packId ClaudeLauncher `
    --packVersion $version `
    --packTitle 'Claude Launcher' `
    --packAuthors 'Moad Dahbi' `
    --packDir publish `
    --mainExe ClaudeLauncher.exe `
    --icon Assets\icon.ico `
    --shortcuts 'Desktop,StartMenuRoot' `
    --outputDir Releases
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

Write-Host "Installer ready: $PSScriptRoot\Releases\ClaudeLauncher-win-Setup.exe"
