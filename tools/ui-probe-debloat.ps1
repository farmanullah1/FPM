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

if (-not (Invoke-NamedElement $window "Apps tab")) { "no Apps tab"; exit 5 }
Start-Sleep -Seconds 1
$apps = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Preinstalled apps")))
if (-not $apps) { "Apps tab content not found"; exit 6 }
"apps tab visible"
