try {
  $z = Get-CimInstance -Namespace root/WMI -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction Stop
  foreach ($zone in $z) {
    $c = ($zone.CurrentTemperature / 10.0) - 273.15
    "WMI zone: $($zone.InstanceName) raw=$($zone.CurrentTemperature) celsius=$([math]::Round($c,1))"
  }
} catch { "WMI thermal zone failed: $($_.Exception.Message)" }

try {
  $s = Get-Counter '\Thermal Zone Information\Temperature' -ErrorAction Stop
  foreach ($sample in $s.CounterSamples) { "PerfCounter: $($sample.InstanceName) kelvin=$($sample.CookedValue) celsius=$([math]::Round($sample.CookedValue - 273.15,1))" }
} catch { "Perf counter failed: $($_.Exception.Message)" }

Get-CimInstance -Namespace root/WMI -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object -First 1 | Format-List *
