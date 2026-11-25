<#
.SYNOPSIS
    Fails when XAML uses spacing or colour literals instead of design tokens.

.DESCRIPTION
    Card U1 requires views and controls to consume Style/Spacing.xaml and the theme
    dictionaries instead of hard-coded numbers and hex colours. This linter counts
    violations per file and compares them with tools/xaml-lint-baseline.json.

    The baseline is a ratchet, not an exemption: a file may not gain violations, and
    cards U3/U10 drive every count down. Run with -Strict (no baseline) once the
    counts reach zero — that is the mode CI uses.

    Allow-listed (the token and art files themselves):
        Styles/Themes/Light.xaml, Styles/Themes/Dark.xaml,
        Styles/Spacing.xaml, Styles/Icons.xaml

.PARAMETER Root
    Repository root. Defaults to the parent of this script.

.PARAMETER UpdateBaseline
    Rewrite the baseline with the current counts.

.PARAMETER Strict
    Ignore the baseline: any violation fails.

.EXAMPLE
    pwsh tools/lint-xaml.ps1
.EXAMPLE
    pwsh tools/lint-xaml.ps1 -Strict
#>
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$UpdateBaseline,
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is empty while a param default is evaluated on PowerShell 5.1.
if (-not $Root) { $Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath) }

$scanRoot = Join-Path $Root 'app/src/PCManager'
if (-not (Test-Path $scanRoot)) {
    throw "Cannot find $scanRoot"
}

$allowList = @(
    'Styles/Themes/Light.xaml',
    'Styles/Themes/Dark.xaml',
    'Styles/Spacing.xaml',
    'Styles/Icons.xaml'
)

$baselinePath = Join-Path $PSScriptRoot 'xaml-lint-baseline.json'

# A literal is a numeric Margin/Padding/CornerRadius, or a hex colour. Anything that
# starts with "{" (a markup extension such as {DynamicResource Space.4}) is a token use.
$spacingPattern = '(Margin|Padding|CornerRadius)="\s*[0-9]'
$colorPattern = '#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?'

$files = Get-ChildItem -Path $scanRoot -Recurse -Filter *.xaml -File | Sort-Object FullName
$counts = @{}
$details = @{}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($scanRoot.Length + 1).Replace('\', '/')
    if ($allowList -contains $relative) { continue }

    $line = 0
    $found = @()
    foreach ($text in Get-Content -LiteralPath $file.FullName) {
        $line++
        if ($text -match $spacingPattern -or $text -match $colorPattern) {
            $found += "$relative`:$line"
        }
    }

    if ($found.Count -gt 0) {
        $counts[$relative] = $found.Count
        $details[$relative] = $found
    }
}

if ($UpdateBaseline) {
    $counts | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $baselinePath -Encoding UTF8
    Write-Host "Baseline written to $baselinePath ($($counts.Count) files)."
    exit 0
}

$baseline = @{}
if (-not $Strict -and (Test-Path $baselinePath)) {
    $raw = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    foreach ($property in $raw.PSObject.Properties) { $baseline[$property.Name] = [int]$property.Value }
}
elseif (-not $Strict) {
    Write-Warning "No baseline at $baselinePath; treating every violation as new."
}

$total = 0
$regressions = @()
$rows = @()

foreach ($path in ($counts.Keys | Sort-Object)) {
    $now = $counts[$path]
    $allowed = if ($baseline.ContainsKey($path)) { $baseline[$path] } else { 0 }
    $total += $now
    $rows += [pscustomobject]@{ File = $path; Now = $now; Allowed = $allowed }
    if ($now -gt $allowed) { $regressions += $path }
}

$rows | Format-Table -AutoSize | Out-String | Write-Host

if ($regressions.Count -gt 0) {
    Write-Host "New literal spacing/colour in:" -ForegroundColor Red
    foreach ($path in $regressions) {
        Write-Host "  $path" -ForegroundColor Red
        foreach ($location in $details[$path]) { Write-Host "    $location" -ForegroundColor DarkGray }
    }
    Write-Host ""
    Write-Host "FAILED: $($regressions.Count) file(s) gained literals. Use tokens from Styles/Spacing.xaml and the theme dictionaries." -ForegroundColor Red
    exit 1
}

Write-Host "OK: $total literal spacing/colour value(s) across $($counts.Count) file(s); none above the baseline." -ForegroundColor Green
if ($total -gt 0) {
    Write-Host "Ratchet note: cards U3/U10 reduce these counts; re-run with -UpdateBaseline after each reduction, and switch CI to -Strict at zero."
}
exit 0
