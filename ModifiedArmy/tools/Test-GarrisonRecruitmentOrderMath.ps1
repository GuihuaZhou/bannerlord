$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot `
    "..\ModifiedArmy\Recruitment\Orders\Models\GarrisonStockPlanningMath.cs"
Add-Type -Path $source

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name expected $expected but observed $actual"
    }
    Write-Host "[PASS] $name"
}

$math = [ModifiedArmy.Recruitment.Orders.Models.GarrisonStockPlanningMath]
$poor = $math::Calculate(200, 50, 0, 75, 200, 0, 80000, 0.5, 200)
Assert-Equal "Poor clan floor target" 75 $poor.Target
Assert-Equal "Poor clan reorder point" 75 $poor.ReorderPoint

$wealthy = $math::Calculate(300, 90, 20, 75, 200, 80000, 80000, 0.5, 300)
Assert-Equal "Wealth weighted target" 137 $wealthy.Target
Assert-Equal "Wealth weighted reorder" 106 $wealthy.ReorderPoint

$capacity = $math::Calculate(100, 76, 50, 75, 200, 80000, 80000, 0.5, 300)
Assert-Equal "Returning escorts reserve hard capacity" 50 $capacity.Target
Assert-Equal "No hard free slots when over reserved capacity" 0 $capacity.HardFreeSlots

Assert-Equal "Transfer out does not expand active order" 30 `
    ($math::ReviewRemaining(30, 160, 70, 160))
Assert-Equal "External arrivals shrink active order" 10 `
    ($math::ReviewRemaining(30, 160, 150, 160))
Assert-Equal "Explicit target growth may expand active order" 50 `
    ($math::ReviewRemaining(30, 180, 100, 160))

Write-Host "Garrison recruitment order math tests passed."
