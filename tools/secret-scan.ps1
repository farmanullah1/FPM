<#
.SYNOPSIS
    Scans the whole repository, including every commit in history, for secrets.

.DESCRIPTION
    Uses gitleaks (pinned below). The binary is taken from PATH or from the FPM tools folder;
    when it is missing it is downloaded from the official GitHub release, its SHA-256 is checked
    against the release checksums file, and it is extracted under %LOCALAPPDATA%\FPM\tools.

    Findings are printed redacted; the report itself is never committed.

.PARAMETER Version
    gitleaks version to use. Keep this in sync with docs/readiness/TOOLS.md and CI.

.PARAMETER ToolsRoot
    Where to install gitleaks when it is missing. Defaults to %LOCALAPPDATA%\FPM\tools.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/secret-scan.ps1
#>
[CmdletBinding()]
param(
    [string]$Version = '8.30.1',
    [string]$ToolsRoot
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $ToolsRoot) {
    $ToolsRoot = Join-Path $env:LOCALAPPDATA 'FPM\tools'
}

function Resolve-Gitleaks {
    $fromPath = Get-Command gitleaks -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $installed = Join-Path $ToolsRoot 'gitleaks\gitleaks.exe'
    if (Test-Path -LiteralPath $installed) { return $installed }

    $archive = "gitleaks_${Version}_windows_x64.zip"
    $base = "https://github.com/gitleaks/gitleaks/releases/download/v$Version"
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("fpm-gitleaks-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp | Out-Null

    try {
        $zip = Join-Path $temp $archive
        $checksums = Join-Path $temp 'checksums.txt'
        Invoke-WebRequest -Uri "$base/$archive" -OutFile $zip -UseBasicParsing
        Invoke-WebRequest -Uri "$base/gitleaks_${Version}_checksums.txt" -OutFile $checksums -UseBasicParsing

        $line = Get-Content -LiteralPath $checksums |
            Where-Object { $_ -match [regex]::Escape($archive) } |
            Select-Object -First 1

        if (-not $line) { throw "No checksum entry for $archive in the release checksums." }

        $expected = ($line -split '\s+')[0].ToLowerInvariant()
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($expected -ne $actual) { throw "SHA-256 mismatch for $archive (expected $expected, got $actual)." }

        $target = Join-Path $ToolsRoot 'gitleaks'
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Expand-Archive -LiteralPath $zip -DestinationPath $target -Force
        return (Join-Path $target 'gitleaks.exe')
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$exe = Resolve-Gitleaks
Write-Output "gitleaks: $(& $exe version)"

# `gitleaks git` scans the git log, so every commit in history is covered.
& $exe git $root --redact --no-banner --verbose --log-opts=--all
$exit = $LASTEXITCODE

if ($exit -eq 0) {
    Write-Output 'secret scan: no leaks found'
}
else {
    Write-Output "secret scan: gitleaks exited $exit (leaks found or scan failed)"
}

exit $exit
