<#
.SYNOPSIS
    Checks the documented foreground/background pairs in both themes.

.DESCRIPTION
    Implements v7 §8.2's contrast gate: body text needs 4.5:1, large text and
    meaningful graphics need 3:1. Reads the SolidColorBrush keys straight from
    Styles/Themes/Light.xaml and Dark.xaml, so the check covers what the app actually
    ships rather than a copy of the palette.

    Exits 1 when any pair fails.

.EXAMPLE
    pwsh tools/contrast-check.ps1
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is empty while a param default is evaluated on PowerShell 5.1.
if (-not $Root) { $Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath) }

function ConvertTo-Luminance([string]$hex) {
    $value = $hex.TrimStart('#')
    if ($value.Length -eq 8) { $value = $value.Substring(2) }   # ignore alpha
    if ($value.Length -ne 6) { throw "Unsupported colour '$hex'" }

    $channels = @(0, 2, 4) | ForEach-Object {
        $c = [Convert]::ToInt32($value.Substring($_, 2), 16) / 255.0
        if ($c -le 0.03928) { $c / 12.92 } else { [Math]::Pow((($c + 0.055) / 1.055), 2.4) }
    }
    return 0.2126 * $channels[0] + 0.7152 * $channels[1] + 0.0722 * $channels[2]
}

function Get-Ratio([string]$foreground, [string]$background) {
    $a = ConvertTo-Luminance $foreground
    $b = ConvertTo-Luminance $background
    $lighter = [Math]::Max($a, $b)
    $darker = [Math]::Min($a, $b)
    return ($lighter + 0.05) / ($darker + 0.05)
}

function Read-Palette([string]$path) {
    $palette = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match 'x:Key="([^"]+)"\s+Color="(#[0-9A-Fa-f]{6,8})"') {
            $palette[$matches[1]] = $matches[2]
        }
    }
    return $palette
}

# fore, back, minimum ratio, what the pair is used for
$pairs = @(
    @('App.Text',         'Surface.1',        4.5, 'body text on a card'),
    @('App.Text',         'Surface.0',        4.5, 'body text on the backdrop'),
    @('App.Text',         'Surface.2',        4.5, 'body text on a raised surface'),
    @('App.Text',         'App.Rail',         4.5, 'nav label on the rail'),
    @('App.Text',         'App.Selection',    4.5, 'selected nav label'),
    @('App.Subtle',       'Surface.1',        4.5, 'secondary text on a card'),
    @('App.Subtle',       'App.Rail',         4.5, 'secondary text on the rail'),
    @('App.AccentLabel',  'Surface.1',        4.5, 'link/metric label'),
    @('App.AccentLabel',  'App.AccentSoft',   4.5, 'label on the accent-soft pill'),
    @('App.AccentText',   'App.Accent',       4.5, 'primary button text'),
    @('App.AccentText',   'App.AccentHover',  4.5, 'primary button hover text'),
    @('App.AccentText',   'App.AccentPressed',4.5, 'primary button pressed text'),
    @('App.Success.Text', 'Surface.1',        4.5, 'success word on a card'),
    @('App.Warning.Text', 'Surface.1',        4.5, 'warning word on a card'),
    @('App.Critical.Text','Surface.1',        4.5, 'critical word on a card'),
    @('App.AccentBar',    'App.Track',        3.0, 'progress fill against its track'),
    @('App.Success.Fill', 'App.Track',        3.0, 'success bar against its track'),
    @('App.Critical.Fill','App.Track',        3.0, 'critical bar against its track'),
    @('Chart.1',          'Surface.1',        3.0, 'chart series 1'),
    @('Chart.2',          'Surface.1',        3.0, 'chart series 2'),
    @('Chart.3',          'Surface.1',        3.0, 'chart series 3'),
    @('Chart.4',          'Surface.1',        3.0, 'chart series 4'),
    @('Chart.5',          'Surface.1',        3.0, 'chart series 5'),
    @('Chart.6',          'Surface.1',        3.0, 'chart series 6')
)

$failures = @()

foreach ($theme in @('Light', 'Dark')) {
    $path = Join-Path $Root "app/src/PCManager/Styles/Themes/$theme.xaml"
    if (-not (Test-Path $path)) { throw "Cannot find $path" }
    $palette = Read-Palette $path

    Write-Host ""
    Write-Host "== $theme ==" -ForegroundColor Cyan

    foreach ($pair in $pairs) {
        $fore = $pair[0]; $back = $pair[1]; $min = [double]$pair[2]; $purpose = $pair[3]

        if (-not $palette.ContainsKey($fore) -or -not $palette.ContainsKey($back)) {
            $missing = if (-not $palette.ContainsKey($fore)) { $fore } else { $back }
            Write-Host ("  {0,-34} MISSING token {1}" -f "$fore on $back", $missing) -ForegroundColor Red
            $failures += "$theme/$purpose ($missing missing)"
            continue
        }

        $ratio = Get-Ratio $palette[$fore] $palette[$back]
        $rounded = [Math]::Round($ratio, 2)
        $ok = $ratio -ge $min
        $status = if ($ok) { 'PASS' } else { 'FAIL' }
        $colour = if ($ok) { 'Green' } else { 'Red' }
        Write-Host ("  {0,-34} {1,5:0.00}:1  >= {2,3}:1  {3}  ({4})" -f "$fore on $back", $rounded, $min, $status, $purpose) -ForegroundColor $colour
        if (-not $ok) { $failures += "$theme/$purpose ($rounded < $min)" }
    }
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "FAILED: $($failures.Count) pair(s) below the required ratio:" -ForegroundColor Red
    foreach ($failure in $failures) { Write-Host "  $failure" -ForegroundColor Red }
    exit 1
}

Write-Host "OK: every documented pair meets its ratio in both themes." -ForegroundColor Green
exit 0
