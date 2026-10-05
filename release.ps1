<#
.SYNOPSIS
    Publishes the version in ClaudeLauncher.csproj as a GitHub release (tag vX.Y.Z).
    Installed copies pick it up automatically on their next launch.

.DESCRIPTION
    1. Bump <Version> in ClaudeLauncher.csproj.
    2. Write release-notes\X.Y.Z.md (shown on the GitHub release page).
    3. Commit and push, then run .\release.ps1
#>
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$repoUrl = 'https://github.com/dahbimoad/ClaudeLauncher'
$version = ([xml](Get-Content ClaudeLauncher.csproj)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
$tag = "v$version"
$notes = "release-notes\$version.md"

if (-not (Test-Path $notes)) { throw "Missing $notes. Write the release notes first." }
if (git status --porcelain) { throw 'Uncommitted changes. Commit and push before releasing.' }
if (git tag --list $tag) { throw "Tag $tag already exists. Bump <Version> in ClaudeLauncher.csproj." }
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw 'git push failed' }

$token = gh auth token
if (-not $token) { throw 'Not logged in to GitHub CLI. Run: gh auth login' }

Remove-Item Releases -Recurse -Force -ErrorAction SilentlyContinue
# Fetching the previous release lets vpk build a small delta package for existing installs.
if (gh release list --repo dahbimoad/ClaudeLauncher --limit 1) {
    vpk download github --repoUrl $repoUrl --token $token --outputDir Releases
    if ($LASTEXITCODE -ne 0) { throw 'vpk download failed' }
}

& "$PSScriptRoot\build-installer.ps1"

vpk upload github --repoUrl $repoUrl --token $token --outputDir Releases `
    --tag $tag --releaseName "Claude Launcher $version" --publish
if ($LASTEXITCODE -ne 0) { throw 'vpk upload failed' }

gh release edit $tag --repo dahbimoad/ClaudeLauncher --notes-file $notes
git fetch --tags --quiet

Write-Host "Released $tag -> $repoUrl/releases/tag/$tag"
