<#
.SYNOPSIS
    Baut VuloPods im Release-Modus und installiert es für den aktuellen Benutzer.

.DESCRIPTION
    - Ziel: %LocalAppData%\Programs\VuloPods (keine Adminrechte nötig)
    - Beendet eine laufende VuloPods-Instanz vor dem Kopieren
    - Legt Verknüpfungen im Startmenü und auf dem Desktop an
    - Biegt einen vorhandenen Autostart-Eintrag auf die installierte EXE um
    - Startet VuloPods danach

    Erneut ausführen = Update. Deinstallieren: scripts\uninstall.ps1
#>
[CmdletBinding()]
param(
    [switch]$NoDesktopShortcut,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $env:LOCALAPPDATA 'Programs\VuloPods'
$exe = Join-Path $target 'VuloPods.exe'
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "VuloPods-publish-$([guid]::NewGuid().ToString('N'))"

Write-Host "Baue VuloPods (Release)..."
dotnet publish (Join-Path $repo 'src\VuloPods.App\VuloPods.App.csproj') -c Release -o $staging --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Exit-Code $LASTEXITCODE)." }

$running = Get-Process VuloPods -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Beende laufendes VuloPods..."
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

Write-Host "Installiere nach $target"
# Windows gibt die EXE nach dem Beenden nicht sofort frei (Handle-Abbau, Virenscanner).
for ($i = 1; (Test-Path $target) -and $i -le 20; $i++) {
    try { Remove-Item $target -Recurse -Force -ErrorAction Stop }
    catch { if ($i -eq 20) { throw "Konnte $target nicht leeren: $($_.Exception.Message)" }; Start-Sleep -Milliseconds 500 }
}
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $staging '*') $target -Recurse -Force
Remove-Item $staging -Recurse -Force

function New-Shortcut([string]$path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $target
    $link.IconLocation = "$exe,0"
    $link.Description = 'VuloPods – AirPods für Windows'
    $link.Save()
}

$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'VuloPods.lnk'
New-Shortcut $startMenu
Write-Host "Startmenü-Verknüpfung: $startMenu"

if (-not $NoDesktopShortcut) {
    $desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'VuloPods.lnk'
    New-Shortcut $desktop
    Write-Host "Desktop-Verknüpfung:   $desktop"
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty $runKey -Name VuloPods -ErrorAction SilentlyContinue) {
    Set-ItemProperty $runKey -Name VuloPods -Value "`"$exe`""
    Write-Host "Autostart-Eintrag auf die installierte Version umgestellt."
}

if (-not $NoStart) {
    Start-Process $exe
    Write-Host "VuloPods läuft (Tray-Icon unten rechts)."
}
