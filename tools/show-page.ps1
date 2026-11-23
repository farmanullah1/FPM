param([string]$Page = "Storage")

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process FarmanullahPCManager -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw "FarmanullahPCManager is not running" }

$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $Page)
$matches = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
"matches=$($matches.Count)"

$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
foreach ($el in $matches) {
    $node = $el
    while ($node -ne $null) {
        $pattern = $null
        if ($node.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $pattern.Select()
            "selected $Page"
            exit 0
        }
        $node = $walker.GetParent($node)
    }
}

throw "No selectable rail item named $Page"
