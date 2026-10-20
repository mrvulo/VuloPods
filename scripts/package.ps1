<#
.SYNOPSIS
    Baut das Release-Paket (self-contained Single-File ZIP plus SHA256SUMS.txt) nach artifacts\.

.DESCRIPTION
    - Version: Parameter -Version, sonst <Version> aus Directory.Build.props
    - Ausgabe: artifacts\VuloPods-<ver>-win-x64.zip (VuloPods.exe, LICENSE, README.md)
    - Ausgabe: artifacts\SHA256SUMS.txt
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
    $Version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($Version)) { throw 'Keine <Version> in Directory.Build.props gefunden.' }
}
$Version = $Version.Trim()

$artifacts = Join-Path $repo 'artifacts'
$zipName = "VuloPods-$Version-win-x64.zip"
$zipPath = Join-Path $artifacts $zipName
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "VuloPods-package-$([guid]::NewGuid().ToString('N'))"

try {
    Write-Host "Baue VuloPods $Version (win-x64, self-contained, Single-File)..."
    dotnet publish (Join-Path $repo 'src\VuloPods.App\VuloPods.App.csproj') `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -o $staging --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Exit-Code $LASTEXITCODE)." }

    $exe = Join-Path $staging 'VuloPods.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "VuloPods.exe wurde nicht erzeugt: $exe" }

    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    Compress-Archive -DestinationPath $zipPath -Path @(
        $exe,
        (Join-Path $repo 'LICENSE'),
        (Join-Path $repo 'README.md')
    )

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $line = "$hash  $zipName"
    [System.IO.File]::WriteAllText((Join-Path $artifacts 'SHA256SUMS.txt'), $line + "`n", (New-Object System.Text.UTF8Encoding($false)))

    Write-Host "Fertig: $zipPath"
    Write-Host $line
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
