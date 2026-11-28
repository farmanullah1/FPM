Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$windows = @()

foreach ($process in Get-Process FPM -ErrorAction SilentlyContinue) {
    $candidate = $root.FindFirst(
        [System.Windows.Automation.TreeScope]::Children,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)))
    if ($candidate) { $windows += $candidate }
}

if ($windows.Count -eq 0) { "no FPM window"; exit 1 }

function Invoke-NamedElement($window, [string]$name) {
    $matches = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))

    foreach ($element in $matches) {
        try {
            $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            return $true
        } catch {
            try {
                $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                return $true
            } catch {
            }
        }
    }

    return $false
}

$window = $null
foreach ($candidate in $windows) {
    if (Invoke-NamedElement $candidate "Debloat") { $window = $candidate; break }
}

if (-not $window) { "no Debloat nav"; exit 2 }
Start-Sleep -Seconds 2

$heading = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Windows Debloat")))

if (-not $heading) { "Debloat page not found"; exit 3 }
"debloat page visible"

$texts = $window.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)))

$plan = $null
foreach ($item in $texts) {
    if ($item.Current.Name -like "Plan:*") { $plan = $item.Current.Name }
}
if (-not $plan) { "plan text not found"; exit 4 }
$plan
$targetStatus = $null
foreach ($item in $texts) {
    if ($item.Current.Name -like "Target:*") { $targetStatus = $item.Current.Name }
}
"target status: $targetStatus"

# D4: typed target picker, preset-file commands and the report-only drift surface.
function Find-Named($window, [string]$name) {
    return $window.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
}

$target = Find-Named $window "Debloat user target"
if (-not $target) { "user target picker not found"; exit 7 }

$targetText = $null
try {
    $selection = $target.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern).GetCurrentSelection()
    if ($selection.Count -gt 0) { $targetText = $selection[0].Current.Name }
} catch {
}
if (-not $targetText) { $targetText = $target.Current.Name }
"target picker: $targetText"

$warning = $null
foreach ($item in $texts) {
    if ($item.Current.Name -like "Changes apply to your signed-in account*" -or
        $item.Current.Name -like "SYSPREP TARGET*" -or
        $item.Current.Name -like "Offline profile*" -or
        $item.Current.Name -like "This account is signed in*" -or
        $item.Current.Name -like "Preset files contain*" -or
        $item.Current.Name -like "Run a drift check*" -or
        $item.Current.Name -like "Drift check:*") {
        $warning = $item.Current.Name
        $warning
    }
}
if (-not $warning) { "target/drift explanatory text not found"; exit 8 }

if (-not (Invoke-NamedElement $window "Refresh profiles")) { "no refresh profiles button"; exit 9 }
Start-Sleep -Milliseconds 700
"profiles refreshed"

foreach ($command in @("Export debloat preset", "Import debloat preset", "Check debloat drift")) {
    if (-not (Find-Named $window $command)) { "missing command: $command"; exit 10 }
    "command present: $command"
}

if (-not (Invoke-NamedElement $window "Apps tab")) { "no Apps tab"; exit 5 }
Start-Sleep -Seconds 1
$apps = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Preinstalled apps")))
if (-not $apps) { "Apps tab content not found"; exit 6 }
"apps tab visible"
