Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$window = $root.FindFirst(
    [System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "FPM")))

if (-not $window) { "no FPM window"; exit 1 }

$matches = $window.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Settings")))

$selected = $false
foreach ($element in $matches) {
    try {
        $item = $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $item.Select()
        $selected = $true
        break
    } catch {
        try {
            $invoke = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invoke.Invoke()
            $selected = $true
            break
        } catch {
        }
    }
}

if (-not $selected) { "no selectable Settings element"; exit 2 }
Start-Sleep -Seconds 2

$heading = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Windows debloat (engine preview)")))

if ($heading) { "card visible" } else { "card not found"; exit 3 }

$texts = $window.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)))

foreach ($item in $texts) {
    $name = $item.Current.Name
    if ($name -like "Plan:*") { $name }
}
