<#
.SYNOPSIS
    Entfernt die mit install.ps1 installierte VuloPods-Version für den aktuellen Benutzer.

.DESCRIPTION
    Beendet VuloPods, löscht %LocalAppData%\Programs\VuloPods, die Verknüpfungen und den
    Autostart-Eintrag. Das Log unter %LocalAppData%\VuloPods bleibt erhalten, außer mit -RemoveLogs.
#>
[CmdletBinding()]
param([switch]$RemoveLogs)

$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\VuloPods'

Get-Process VuloPods -ErrorAction SilentlyContinue | Stop-Process -Force

foreach ($p in @(
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'VuloPods.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'VuloPods.lnk'),
    $target)) {
    if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Host "Entfernt: $p" }
}

Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name VuloPods -ErrorAction SilentlyContinue

if ($RemoveLogs) {
    $logs = Join-Path $env:LOCALAPPDATA 'VuloPods'
    if (Test-Path $logs) { Remove-Item $logs -Recurse -Force; Write-Host "Entfernt: $logs" }
}

Write-Host "VuloPods ist deinstalliert."
