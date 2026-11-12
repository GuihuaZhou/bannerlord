param([string] $ConfigPath = (Join-Path $PSScriptRoot '..\ModifiedPolitics\ModuleData\settlement_economy.xml'))
$ErrorActionPreference = 'Stop'
[xml] $xml = Get-Content -Raw -LiteralPath $ConfigPath
$population = $xml.SettlementEconomyConfigs.SettlementEconomyConfig.Population
$expected = @{ empire=@(700,800,2800); aserai=@(550,650,2600); vlandia=@(900,750,1850); sturgia=@(650,600,1650); khuzait=@(550,500,1500); battania=@(650,450,1200); nord=@(550,400,1000) }

function Assert-Close([string] $Name, [double] $Actual, [double] $Expected, [double] $Tolerance = 0.0001) {
    if ([double]::IsNaN($Actual) -or [double]::IsInfinity($Actual) -or [math]::Abs($Actual - $Expected) -gt $Tolerance) { throw "$Name expected $Expected but was $Actual" }
}

function Forecast([double] $H, [double] $K, [double] $B, [double] $P, [double] $F, [double] $C) {
    $rate=[double]$population.PopulationRates.town; $seed=[double]$population.NaturalGrowth.seed
    $natural=$rate*($H+$seed)*(1-$H/$K)
    if ($natural -gt 0) {
        $threshold = $K * ([double]$population.NaturalGrowth.GetAttribute('lowPopulationThreshold'))
        $recovery = 1.0 + (([double]$population.NaturalGrowth.GetAttribute('lowPopulationMultiplier')) - 1.0) * [math]::Max(0.0, 1.0 - $H / $threshold)
        $natural = $natural * $recovery
    }
    $food=[math]::Max([double]$population.DailyFactors.foodMin,[math]::Min([double]$population.DailyFactors.foodMax,$F*[double]$population.DailyFactors.foodPerChange))
    $dynamic=$C*$H; $final=$C*$K; $prosperity=0
    if ($H -lt $K -and $P -gt $dynamic -and $P -lt $final) { $prosperity=[math]::Min([double]$population.DailyFactors.prosperityMax,[double]$population.DailyFactors.prosperityMax*($P-$dynamic)/[double]$population.DailyFactors.prosperityTriggerSpan) }
    $ratio=[math]::Max(0.0,[math]::Min(1.0,$B/[math]::Max($H,1.0))); $burden=-[double]$population.DailyFactors.burdenPerCapacity*$K*$ratio
    $raw=$natural+$food+$prosperity+$burden; $applied=if($H -gt $K){[math]::Min($raw,$natural)}else{[math]::Max(0.0,[math]::Min($K,$H+$raw))-$H}
    [pscustomobject]@{Natural=$natural;Food=$food;Prosperity=$prosperity;Burden=$burden;Applied=$applied;Support=$C*[math]::Max(0.0,$H-$B);Dynamic=$dynamic;Final=$final}
}

$cultures=@($population.Cultures.Culture); if($cultures.Count -ne 7){throw 'H01 expected 7 cultures'}
foreach($culture in $cultures){$id=([string]$culture.id)-replace '^Culture\.','';if(-not $expected.ContainsKey($id)){throw "H01 unexpected culture $id"};Assert-Close "H01/$id/village" ([double]$culture.villageCapacity) $expected[$id][0];Assert-Close "H01/$id/castle" ([double]$culture.castleCapacity) $expected[$id][1];Assert-Close "H01/$id/town" ([double]$culture.townCapacity) $expected[$id][2]}
$wall=@([double]$population.WallCapacityFactors.level1,[double]$population.WallCapacityFactors.level2,[double]$population.WallCapacityFactors.level3);if($wall[0] -gt $wall[1] -or $wall[1] -gt $wall[2]){throw 'H05 wall factors are not monotonic'}
$h17=Forecast 1200 2800 200 6500 -20 5
Assert-Close H17/Natural $h17.Natural 1.697959184 0.0001; Assert-Close H17/Food $h17.Food -1; Assert-Close H17/Prosperity $h17.Prosperity 0.6; Assert-Close H17/Burden $h17.Burden -0.14; Assert-Close H17/Total $h17.Applied 1.157959184 0.0001; Assert-Close H17/Support $h17.Support 5000; Assert-Close H17/Dynamic $h17.Dynamic 6000; Assert-Close H17/Final $h17.Final 14000
Assert-Close H10/Food (Forecast 1200 2800 0 6000 -20 5).Food -1; Assert-Close H11/Plus10 (Forecast 1200 2800 0 6000 10 5).Food 0.5; Assert-Close H11/Plus100 (Forecast 1200 2800 0 6000 100 5).Food 2; Assert-Close H11/Minus100 (Forecast 1200 2800 0 6000 -100 5).Food -2
Assert-Close H13/AtDynamic (Forecast 1200 2800 0 6000 0 5).Prosperity 0; Assert-Close H15/AtFinal (Forecast 1200 2800 0 14000 0 5).Prosperity 0
if((Forecast 2800 2800 0 14000 100 5).Applied -gt 0.0001){throw 'H07 exceeded capacity'};if((Forecast 3000 2800 0 14000 100 5).Applied -ge 0){throw 'H08 did not decline'}
$h09=Forecast 0 2800 200 1000 0 5;if([double]::IsNaN($h09.Applied)-or[double]::IsInfinity($h09.Applied)){throw 'H09 produced non-finite output'}
$h12=Forecast 100 2800 0 1000 -1 5;if($h12.Applied -le 0){throw 'H12 negative food incorrectly short-circuited total growth'}
Assert-Close H14/Prosperity (Forecast 1200 2800 0 6500 0 5).Prosperity 0.6
$h16a=Forecast 1200 2800 0 6500 0 5;$h16b=Forecast 1200 2800 200 6500 0 5;Assert-Close H16/Dynamic $h16b.Dynamic $h16a.Dynamic;Assert-Close H16/Final $h16b.Final $h16a.Final;if($h16b.Support -ge $h16a.Support){throw 'H16 burden did not lower support'}
$h18=Forecast 1200 2800 1300 6500 0 5;Assert-Close H18/Support $h18.Support 0
$h20a=Forecast 1200 2800 200 6500 -20 5;$h20b=Forecast 1200 2800 200 6500 -20 5;Assert-Close H20/Idempotent $h20a.Applied $h20b.Applied
Write-Output 'PASS: deterministic XML and hearth math checks (H01-H05, H07-H18, H20). H06 and H19 are covered by resolver/source validation.'
