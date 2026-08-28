<#
.SYNOPSIS
Builds and deploys the ZedWorkspaces plugin to PowerToys Run.

.DESCRIPTION
Default behavior: builds Debug x64 and copies the whole output folder into
%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\ZedWorkspaces\,
then reminds you to fully restart PowerToys.

With -Link it creates a directory junction from the plugins folder to the build
output instead of copying, so every future build is "deployed" automatically.

With -RestartPowerToys it quits PowerToys before copying (to release locked
DLLs) and relaunches it when done.

.EXAMPLE
# build Debug x64 + copy + remind to restart
.\tools\build-deploy.ps1

.EXAMPLE
# build Release ARM64 and deploy via a junction
.\tools\build-deploy.ps1 -Configuration Release -Platform ARM64 -Link

.EXAMPLE
# rebuild and hot-deploy, letting the script restart PowerToys
.\tools\build-deploy.ps1 -RestartPowerToys
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    [switch]$NoBuild,
    [switch]$Link,
    [switch]$RestartPowerToys
)

$ErrorActionPreference = 'Stop'

$repoRoot    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$project     = Join-Path $repoRoot 'src\ZedWorkspaces\ZedWorkspaces.csproj'
$rid         = 'win-' + $Platform.ToLowerInvariant()
$outputDir   = Join-Path $repoRoot "src\ZedWorkspaces\bin\$Platform\$Configuration\net9.0-windows10.0.26100.0\$rid"
$pluginsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins'
$targetDir   = Join-Path $pluginsRoot 'ZedWorkspaces'

function Get-IsReparsePoint {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    return ((Get-Item $Path -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
}

function Test-PowerToysRunning {
    return [bool](Get-Process -Name 'PowerToys' -ErrorAction SilentlyContinue)
}

function Stop-PowerToys {
    $proc = Get-Process -Name 'PowerToys' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { Write-Host 'PowerToys is not running.'; return }

    Write-Host "Stopping PowerToys (PID $($proc.Id))..."
    if (-not $proc.CloseMainWindow()) { $proc | Stop-Process -Force }
    try { if (-not $proc.WaitForExit(3000)) { $proc | Stop-Process -Force } }
    catch { $proc | Stop-Process -Force }
    Start-Sleep -Milliseconds 500
}

function Start-PowerToys {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\PowerToys.exe'),
        'C:\Program Files\PowerToys\PowerToys.exe'
    )
    $exe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($exe) {
        Start-Process $exe
        Write-Host "Started PowerToys: $exe"
    }
    else {
        Write-Host 'PowerToys is stopped - start it manually (Start Menu or the tray shortcut).'
    }
}

function New-DeployLink {
    param([string]$Target, [string]$Value)
    if (Test-Path $Target) {
        if (Get-IsReparsePoint $Target) {
            # removing a junction only removes the link, never the target contents
            Remove-Item $Target -Force
        }
        else {
            throw "Refusing to replace real folder '$Target' with a junction. Delete it manually first."
        }
    }
    New-Item -ItemType Junction -Path $Target -Value $Value | Out-Null
    Write-Host "Created junction: $Target -> $Value"
}

# --- build -----------------------------------------------------------
if (-not $NoBuild) {
    Write-Host "Building $project ($Configuration, $Platform) ..."
    dotnet build $project -c $Configuration -p:Platform=$Platform -r $rid
    if ($LASTEXITCODE -ne 0) { throw 'Build failed - see output above.' }
}

if (-not (Test-Path $outputDir)) {
    throw "Build output not found: $outputDir"
}

# --- release locked files before copying when asked -------------------
if ($RestartPowerToys) { Stop-PowerToys }

if (-not (Test-Path $pluginsRoot)) { New-Item -ItemType Directory -Path $pluginsRoot -Force | Out-Null }

# --- deploy -----------------------------------------------------------
if ($Link) {
    New-DeployLink -Target $targetDir -Value $outputDir
}
else {
    try {
        Write-Host "Deploying: $outputDir -> $targetDir"
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        Copy-Item (Join-Path $outputDir '*') -Destination $targetDir -Recurse -Force
        Write-Host 'Deployed.'
    }
    catch {
        if (Test-PowerToysRunning) {
            Write-Host "Copy failed because PowerToys is running and has a file locked.`nRe-run with -RestartPowerToys, or quit PowerToys first."
        }
        throw
    }
}

# --- finishing -------------------------------------------------------
if ($RestartPowerToys) {
    Start-PowerToys
    Write-Host 'Done.'
}
else {
    Write-Host ''
    Write-Host 'Fully restart PowerToys (tray icon -> Quit, then start it again) for the change to take effect.'
}
