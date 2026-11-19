<#
.SYNOPSIS
    End-to-end build, publish, Windows registration, optional Git commit/push, and relaunch pipeline for SonicDeck.

.DESCRIPTION
    Performs the full SonicDeck release cycle automatically:
      1. Locates dotnet SDK automatically (PATH, %USERPROFILE%\.dotnet, Program Files).
      2. Verifies a clean Release build BEFORE stopping the currently running app.
      3. Gracefully stops any running SonicDeck process so publish\SonicDeck.exe is unlocked.
      4. Publishes self-contained win-x64 build to .\publish\.
      5. Ensures Windows App Paths (Win+R -> 'sonicdeck') and Start Menu shortcut point to publish\SonicDeck.exe.
      6. Optionally commits (-Commit "message") and pushes (-Push) via git or wsl git.
      7. Relaunches publish\SonicDeck.exe unless -NoLaunch is specified.

.EXAMPLE
    .\release.ps1
    .\release.ps1 -Commit "Update Event Horizon shader" -Push
    .\release.ps1 -NoLaunch
#>
[CmdletBinding()]
param(
    [string]$Commit = "",
    [switch]$Push,
    [switch]$NoLaunch,
    [switch]$SkipShortcutSync
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj   = Join-Path $repoRoot "SonicDeck.csproj"
$pubDir   = Join-Path $repoRoot "publish"
$pubExe   = Join-Path $pubDir "SonicDeck.exe"

function Resolve-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        (Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"),
        "C:\Program Files\dotnet\dotnet.exe",
        (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    throw "Could not locate dotnet.exe. Install .NET 8 SDK or add it to PATH."
}

function Invoke-Git {
    param([string[]]$GitArgs)

    $nativeGit = Get-Command git -ErrorAction SilentlyContinue
    if ($nativeGit) {
        Push-Location $repoRoot
        try {
            & $nativeGit.Source @GitArgs
            if ($LASTEXITCODE -ne 0) { throw "git failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }
        return
    }

    $wsl = Get-Command wsl -ErrorAction SilentlyContinue
    if ($wsl) {
        $drive   = $repoRoot.Substring(0, 1).ToLowerInvariant()
        $rest    = $repoRoot.Substring(2).Replace('\', '/')
        $wslPath = "/mnt/$drive$rest"
        $escapedArgs = ($GitArgs | ForEach-Object { "'" + $_.Replace("'", "'""'""'") + "'" }) -join ' '
        & $wsl.Source -e bash -lc "cd '$wslPath' && git $escapedArgs"
        if ($LASTEXITCODE -ne 0) { throw "wsl git failed with exit code $LASTEXITCODE" }
        return
    }

    throw "Neither native git nor WSL git was found."
}

Write-Host ""
Write-Host "=== SonicDeck End-to-End Release Pipeline ===" -ForegroundColor Cyan

# 1. Locate dotnet
$dotnet = Resolve-Dotnet
Write-Host "[1/6] Using dotnet: $dotnet" -ForegroundColor DarkGray

# 2. Pre-flight Release build check (so a broken compile never kills the running app)
Write-Host "[2/6] Verifying Release build..." -ForegroundColor Yellow
& $dotnet build $csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed. Aborting publish so your running SonicDeck remains intact."
}

# 3. Stop any running SonicDeck instances to release file locks on publish\SonicDeck.exe
$running = Get-Process -Name "SonicDeck" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[3/6] Stopping running SonicDeck process(es)..." -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    # Wait briefly until executable handle is completely released
    for ($i = 0; $i -lt 20; $i++) {
        if (-not (Get-Process -Name "SonicDeck" -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 150
    }
    Start-Sleep -Milliseconds 250
} else {
    Write-Host "[3/6] No running SonicDeck process detected." -ForegroundColor DarkGray
}

# 4. Publish self-contained win-x64 build
Write-Host "[4/6] Publishing self-contained win-x64 to .\publish\..." -ForegroundColor Yellow
& $dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $pubDir --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
if (-not (Test-Path $pubExe)) {
    throw "Expected published binary not found at $pubExe"
}

# 5. Keep Windows Start -> Run ('sonicdeck') and Start Menu shortcut synced
if (-not $SkipShortcutSync) {
    Write-Host "[5/6] Syncing Windows App Paths (Win+R -> 'sonicdeck') & Start Menu shortcut..." -ForegroundColor Yellow

    $regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\sonicdeck.exe"
    New-Item -Path $regPath -Force | Out-Null
    Set-ItemProperty -Path $regPath -Name "(Default)" -Value $pubExe
    Set-ItemProperty -Path $regPath -Name "Path" -Value $pubDir

    $sh = New-Object -ComObject WScript.Shell
    $lnkPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SonicDeck.lnk"
    $lnk = $sh.CreateShortcut($lnkPath)
    $lnk.TargetPath = $pubExe
    $lnk.WorkingDirectory = $pubDir
    $lnk.IconLocation = "$pubExe,0"
    $lnk.Description = "SonicDeck - System Audio Controller & Visualizer"
    $lnk.Save()
} else {
    Write-Host "[5/6] Skipping Windows shortcut sync (-SkipShortcutSync)." -ForegroundColor DarkGray
}

# 6. Optional Git commit & push (explicitly excluding local .agents/ folder)
if (-not [string]::IsNullOrWhiteSpace($Commit)) {
    Write-Host "[6/6] Committing changes to Git..." -ForegroundColor Yellow
    Invoke-Git @("add", "-A", "--", ":!.agents")
    Invoke-Git @("commit", "-m", $Commit)
    if ($Push) {
        Write-Host "      Pushing to remote..." -ForegroundColor Yellow
        Invoke-Git @("push")
    }
} elseif ($Push) {
    Write-Host "[6/6] Pushing existing commits to remote..." -ForegroundColor Yellow
    Invoke-Git @("push")
} else {
    Write-Host "[6/6] Git step skipped (pass -Commit 'msg' and/or -Push to auto-commit/push)." -ForegroundColor DarkGray
}

# Relaunch published app
$exeInfo = Get-Item $pubExe
Write-Host ""
Write-Host "Published: $($exeInfo.FullName) ($($exeInfo.LastWriteTime))" -ForegroundColor Green

if (-not $NoLaunch) {
    Write-Host "Launching SonicDeck..." -ForegroundColor Cyan
    Start-Process -FilePath $pubExe -WorkingDirectory $pubDir
}

Write-Host "Done!" -ForegroundColor Green
