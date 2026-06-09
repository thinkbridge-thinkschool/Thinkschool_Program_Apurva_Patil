$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\")).Path
$unitProj = (Join-Path $repoRoot "day4/Task1/Day3/Task6/Quotes.Tests.Unit/Quotes.Tests.Unit.csproj")
$intProj  = (Join-Path $repoRoot "day4/Task1/Day3/Task6/Quotes.Tests.Integration/Quotes.Tests.Integration.csproj")

$unitOut = dotnet test $unitProj --collect:"XPlat Code Coverage" --nologo -v m 2>&1 | Tee-Object -Variable unitLog
$intOut  = dotnet test $intProj  --collect:"XPlat Code Coverage" --nologo -v m 2>&1 | Tee-Object -Variable intLog

function Get-TestTotals([string[]]$log){
  $line = ($log | Select-String -Pattern 'Total tests:\s*\d+\.\s*Passed:\s*\d+\.\s*Failed:\s*\d+\.\s*Skipped:\s*\d+\.' | Select-Object -Last 1).Line
  if(-not $line){ return [PSCustomObject]@{Total='?';Passed='?';Failed='?';Skipped='?';Raw='Not found'} }
  $m=[regex]::Match($line,'Total tests:\s*(\d+)\.\s*Passed:\s*(\d+)\.\s*Failed:\s*(\d+)\.\s*Skipped:\s*(\d+)\.')
  [PSCustomObject]@{Total=[int]$m.Groups[1].Value;Passed=[int]$m.Groups[2].Value;Failed=[int]$m.Groups[3].Value;Skipped=[int]$m.Groups[4].Value;Raw=$line}
}

$unitTotals=Get-TestTotals $unitLog
$intTotals=Get-TestTotals $intLog

function Get-LatestNonEmptyCoverage([string]$rootPath){
  $files = Get-ChildItem -Path $rootPath -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending

  foreach($f in $files){
    try {
      [xml]$xml = Get-Content -Raw $f.FullName
      if($xml.coverage -and [int]$xml.coverage.'lines-valid' -gt 0){
        return $f
      }
    } catch {
      # ignore malformed files and continue looking
    }
  }

  return $null
}

$unitCov = Get-LatestNonEmptyCoverage (Split-Path -Path $unitProj -Parent)
$intCov  = Get-LatestNonEmptyCoverage (Split-Path -Path $intProj -Parent)
if(-not $unitCov -or -not $intCov){ throw 'Coverage file not found for one or both projects.' }

function Add-ReportToMap($path,[hashtable]$map){
  [xml]$xml=Get-Content -Raw $path
  foreach($c in $xml.coverage.packages.package.classes.class){
    $className=[string]$c.name
    $file=[string]$c.filename
    foreach($ln in $c.lines.line){
      $num=[int]$ln.number
      $hit=([int]$ln.hits -gt 0)
      $key="$className|$num"
      if($map.ContainsKey($key)){
        $map[$key].Covered = $map[$key].Covered -or $hit
      } else {
        $map[$key]=[PSCustomObject]@{Class=$className;File=$file;Line=$num;Covered=$hit}
      }
    }
  }
}

$merged=@{}
Add-ReportToMap $unitCov.FullName $merged
Add-ReportToMap $intCov.FullName $merged

$rows=$merged.Values
$valid=$rows.Count
$covered=($rows | Where-Object Covered).Count
$pct = if($valid -gt 0){ [math]::Round((100.0*$covered/$valid),2) } else { 0 }

$groups = $rows | Group-Object {
  $f=($_.File -replace '\\','/')
  if($f -match '(^|/)obj/'){ 'obj/' }
  elseif($f -match '(^|/)Migrations/'){ 'Migrations/' }
  else { 'other' }
} | ForEach-Object {
  $v=$_.Count
  $c=($_.Group | Where-Object Covered).Count
  [PSCustomObject]@{Group=$_.Name;Covered=$c;Valid=$v;Percent=if($v -gt 0){[math]::Round(100.0*$c/$v,2)}else{0}}
} | Sort-Object Group

$progUncovered = $rows | Where-Object { -not $_.Covered -and $_.Class -match '(^|\.)Program$' } | Sort-Object Line | Select-Object -ExpandProperty Line -Unique
$clockUncovered = $rows | Where-Object { -not $_.Covered -and $_.Class -match '(^|\.)SystemClock$' } | Sort-Object Line | Select-Object -ExpandProperty Line -Unique

Write-Output "Unit coverage file: $($unitCov.FullName)"
Write-Output "Integration coverage file: $($intCov.FullName)"
Write-Output ""
Write-Output "Test totals:"
Write-Output "- Unit: Total=$($unitTotals.Total) Passed=$($unitTotals.Passed) Failed=$($unitTotals.Failed) Skipped=$($unitTotals.Skipped)"
Write-Output "- Integration: Total=$($intTotals.Total) Passed=$($intTotals.Passed) Failed=$($intTotals.Failed) Skipped=$($intTotals.Skipped)"
Write-Output ""
Write-Output "Merged line coverage (class+line): Covered=$covered Valid=$valid Percent=$pct%"
Write-Output ""
Write-Output "Grouped totals:"
$groups | Format-Table -AutoSize | Out-String | Write-Output
$progUncoveredText = if($progUncovered){ $progUncovered -join ', ' } else { '<none>' }
$clockUncoveredText = if($clockUncovered){ $clockUncovered -join ', ' } else { '<none>' }
Write-Output "Uncovered Program lines: $progUncoveredText"
Write-Output "Uncovered SystemClock lines: $clockUncoveredText"
