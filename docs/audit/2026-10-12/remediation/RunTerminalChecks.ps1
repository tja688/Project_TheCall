param(
  [ValidateSet('VictoryAndFailure', 'FailureOnly')][string]$Mode = 'VictoryAndFailure',
  [int]$Seed = 101,
  [int]$PresentationTimeoutSeconds = 600,
  [string]$ProjectRoot = 'C:/Users/jinji/Documents/GitHub/Project_TheCall',
  [string]$OutputDirectory = ''
)

# Run after the three natural player plans, while the existing Editor remains in Play Mode.
# No model writes, resource injection, phase changes, fixture creation, or presentation deletion.
# Mutations are exclusively M5's Begin (fixed-seed new game) and actual Button.onClick inputs.
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'evidence/terminal-checks' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$script:Checks = [Collections.Generic.List[object]]::new()
$script:Failures = [Collections.Generic.List[string]]::new()
$evalLog = Join-Path $OutputDirectory 'editor-eval.jsonl'

function Invoke-EditorEval([string]$Code) {
  $raw = & unity --no-banner --non-interactive command eval --project-path $ProjectRoot --caller plugin --skill unity-cli --format json -- $Code 30000
  $exitCode = $LASTEXITCODE
  $result = ($raw -join "`n") | ConvertFrom-Json
  @{ time = (Get-Date).ToString('o'); code = $Code; exit = $exitCode; response = $result } |
    ConvertTo-Json -Depth 30 -Compress | Add-Content -LiteralPath $evalLog -Encoding utf8
  if ($exitCode -ne 0 -or -not $result.success -or -not $result.data.result.success) {
    throw ($result | ConvertTo-Json -Depth 12)
  }
  return $result.data.result.result
}
function Assert-Check([bool]$Condition, [string]$Name, [object]$Actual) {
  $script:Checks.Add([ordered]@{ name = $Name; passed = $Condition; actual = $Actual })
  if (-not $Condition) { $script:Failures.Add($Name) }
}
function Get-Snapshot([string]$Label) {
  return Invoke-EditorEval ('return TheCall.Editor.M5PlayModeAutomation.Snapshot("' + $Label + '");')
}
function Get-Frame([string]$Snapshot) {
  if ($Snapshot -notmatch 'frame=(\d+)') { throw "Frame missing: $Snapshot" }
  return [int]$Matches[1]
}
function Wait-Frames([int]$AfterFrame, [string]$Label) {
  $deadline = (Get-Date).AddSeconds(30)
  do {
    Start-Sleep -Milliseconds 300
    $snapshot = Get-Snapshot $Label
    if ((Get-Frame $snapshot) -gt $AfterFrame) { return $snapshot }
  } while ((Get-Date) -lt $deadline)
  throw "Unity frame stalled after $AfterFrame ($Label)"
}
function Wait-Presentation([int]$AfterFrame, [string]$Label) {
  $deadline = (Get-Date).AddSeconds($PresentationTimeoutSeconds)
  $lastAdvanced = Get-Date
  $lastFrame = $AfterFrame
  do {
    Start-Sleep -Seconds 2
    $snapshot = Get-Snapshot $Label
    $frame = Get-Frame $snapshot
    if ($frame -gt $lastFrame) { $lastAdvanced = Get-Date; $lastFrame = $frame }
    if ($snapshot -match 'playback=False' -and $frame -gt $AfterFrame) { return $snapshot }
    if (((Get-Date) - $lastAdvanced).TotalSeconds -gt 30) { throw "Unity stopped advancing: $snapshot" }
  } while ((Get-Date) -lt $deadline)
  throw "Natural presentation timed out: $snapshot"
}
function Get-FullState([string]$Label) {
  $state = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.State();'
  $code = @'
var app = TheCall.TheCallApp.Interface;
var type = typeof(TheCall.TheCallApp).Assembly.GetType("TheCall.RunModel");
var run = app.GetType().GetMethod("GetModel").MakeGenericMethod(type).Invoke(app, new object[0]);
var names = new[] { "_shelfMonsters", "_shelfTools", "_dailyAdds", "_dailyLegacyAdds", "_workedToday" };
var extra = new System.Text.StringBuilder();
foreach (var name in names) {
    var value = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(run);
    extra.Append(name).Append("=").Append(value.GetType().GetProperty("Count").GetValue(value)).Append(";");
}
extra.Append("candidates=").Append(app.SendQuery(new TheCall.OpeningCandidatesQuery()).Count).Append(";");
extra.Append("geneCapacity=").Append(app.SendQuery(new TheCall.RunLedgerQuery()).GeneCapacity).Append(";");
extra.Append("productionCount=").Append(app.SendQuery(new TheCall.ProductionLogQuery()).Count).Append(";");
extra.Append("rngType=").Append(app.GetUtility<TheCall.IDraw>().GetType().FullName).Append(";");
return extra.ToString();
'@
  $extra = Invoke-EditorEval $code
  $full = $state + "`n" + $extra
  Set-Content -LiteralPath (Join-Path $OutputDirectory ($Label + '.txt')) -Value $full -Encoding utf8
  $fields = @{}
  foreach ($pair in ($full -split '[;\r\n]+')) {
    if ($pair -match '^([^=]+)=(.*)$') { $fields[$Matches[1]] = $Matches[2] }
  }
  return @{ Text = $full; Fields = $fields }
}
function Assert-EmptyTerminal([string]$ExpectedPhase, [string]$Label) {
  $state = Get-FullState $Label
  $fields = $state.Fields
  Assert-Check ($fields.phase -eq $ExpectedPhase) "$Label.phase" $fields.phase
  foreach ($key in @('energy','due','gold','tech','retained','_shelfMonsters','_shelfTools','_dailyAdds','_dailyLegacyAdds','_workedToday','candidates')) {
    Assert-Check ($fields[$key] -eq '0') "$Label.$key.reset" $fields[$key]
  }
  Assert-Check ($fields.overtime -eq 'False') "$Label.overtime.reset" $fields.overtime
  Assert-Check ($fields.tools -eq '' -and $fields.research -eq '') "$Label.tools-research.reset" "$($fields.tools)|$($fields.research)"
  Assert-Check ($fields.cage -eq '') "$Label.cage.empty" $fields.cage
  Assert-Check ($fields.extraction -eq ',,,,' -and $fields.breeding -eq ',') "$Label.capacity-reset-and-empty" "$($fields.extraction)|$($fields.breeding)"
  Assert-Check ($fields.geneCapacity -eq '3') "$Label.gene-capacity.reset" $fields.geneCapacity
  Assert-Check ($state.Text -notmatch '(?m)^monster=|^skill=|^gene=') "$Label.monsters-skills-genes.empty" $state.Text
  return $state
}
function Assert-NewOpening([string]$Label) {
  $state = Get-FullState $Label
  $fields = $state.Fields
  Assert-Check ($fields.phase -eq 'Opening' -and $fields.day -eq '0') "$Label.opening-day0" "$($fields.phase)|$($fields.day)"
  foreach ($key in @('energy','due','gold','tech','retained','_shelfMonsters','_shelfTools','_dailyAdds','_dailyLegacyAdds','_workedToday','productionCount')) {
    Assert-Check ($fields[$key] -eq '0') "$Label.$key.reset" $fields[$key]
  }
  Assert-Check ($fields.candidates -eq '3') "$Label.three-new-candidates" $fields.candidates
  Assert-Check ($fields.geneCapacity -eq '3') "$Label.gene-capacity3" $fields.geneCapacity
  Assert-Check ($fields.tools -eq '' -and $fields.research -eq '' -and $fields.cage -eq '') "$Label.inventory.empty" "$($fields.tools)|$($fields.research)|$($fields.cage)"
  Assert-Check ($fields.extraction -eq ',,,,' -and $fields.breeding -eq ',') "$Label.operation-capacity.reset" "$($fields.extraction)|$($fields.breeding)"
  Assert-Check (($state.Text -split "`n" | Where-Object { $_ -match '^monster=' }).Count -eq 3) "$Label.only-three-candidate-monsters" $state.Text
  Assert-Check ($state.Text -notmatch ';work=[1-9]|;rest=[1-9]|;clocked=True|;worked=True|^gene=') "$Label.no-prior-history-reservations" $state.Text
  Assert-Check ($state.Text -notmatch ';temporary=True|;immovable=True|;capacity=[1-9]|;parents=m') "$Label.no-prior-instance-or-monster-modifications" $state.Text
  return $state
}
function Save-Screenshot([string]$Name) {
  $before = Get-Frame (Get-Snapshot ('before-capture-' + $Name))
  $source = Invoke-EditorEval ('return TheCall.Editor.M5PlayModeAutomation.CaptureScreenshot("' + $Name + '.png");')
  $null = Wait-Frames $before ('after-capture-' + $Name)
  $deadline = (Get-Date).AddSeconds(30)
  while (-not (Test-Path -LiteralPath $source) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
  if (-not (Test-Path -LiteralPath $source)) { throw "Screenshot was not written: $source" }
  $destination = Join-Path $OutputDirectory ($Name + '.png')
  Copy-Item -LiteralPath $source -Destination $destination -Force
  Assert-Check ((Get-Item -LiteralPath $destination).Length -gt 0) "$Name.screenshot-written" $destination
  return $destination
}

try {
  & unity --version | Set-Content -LiteralPath (Join-Path $OutputDirectory 'unity-cli-version.txt') -Encoding utf8
  & unity --no-banner --non-interactive status --format json --project-path $ProjectRoot |
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'editor-status.json') -Encoding utf8
  $ready = Invoke-EditorEval 'return UnityEngine.Application.isPlaying + "|" + TheCall.Editor.M5PlayModeAutomation.Playing + "|" + TheCall.Editor.M5PlayModeAutomation.PlanStatus;'
  if ($ready -notmatch '^True\|False\|' -or $ready -match '\|running ') { throw "Wait until the prior player plan and presentation finish: $ready" }

  # Begin resets M5 trace text; preserve the existing natural-run evidence first.
  $priorRoot = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.EvidenceRoot;'
  if (Test-Path -LiteralPath $priorRoot) {
    Copy-Item -LiteralPath $priorRoot -Destination (Join-Path $OutputDirectory 'archive-before-terminal') -Recurse -Force
  }
  if ($Mode -eq 'VictoryAndFailure') {
    $victory = Assert-EmptyTerminal 'Victory' 'victory-day7'
    Assert-Check ($victory.Fields.day -eq '7') 'victory.no-eighth-day' $victory.Fields.day
    $null = Save-Screenshot 'terminal-victory'
    $before = Get-Frame (Get-Snapshot 'before-victory-restart')
    $null = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.Click("restart");'
    $null = Wait-Frames $before 'after-victory-restart'
    $null = Assert-NewOpening 'restart-after-victory'
    $null = Save-Screenshot 'restart-after-victory'
  }

  $null = Invoke-EditorEval ('return TheCall.Editor.M5PlayModeAutomation.Begin(' + $Seed + ');')
  $candidate = Invoke-EditorEval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.OpeningCandidatesQuery())[0].Id;'
  $null = Invoke-EditorEval ('return TheCall.Editor.M5PlayModeAutomation.Click("opening", "' + $candidate + '");')
  $opening = Get-FullState 'failure-empty-day1-start'
  Assert-Check ($opening.Fields.phase -eq 'Operation' -and $opening.Fields.day -eq '1') 'failure.starts-in-day1-operation' $opening.Text
  Assert-Check ($opening.Fields.extraction -eq ',,,,') 'failure.empty-extraction-player-input' $opening.Fields.extraction
  Assert-Check ($opening.Fields.tools -eq '' -and $opening.Fields.gold -eq '0' -and $opening.Fields.tech -eq '0') 'failure.no-tools-or-injected-resources' $opening.Text

  $before = Get-Frame (Get-Snapshot 'before-failure-round1')
  $null = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.Click("settle");'
  $null = Wait-Presentation $before 'failure-round1-natural-playback'
  $overtime = Get-FullState 'failure-round1-overtime'
  Assert-Check ($overtime.Fields.phase -eq 'Operation' -and $overtime.Fields.overtime -eq 'True') 'failure.first-round-reaches-overtime' $overtime.Text
  Assert-Check ($overtime.Fields.energy -eq '0' -and $overtime.Fields.gold -eq '0') 'failure.first-round-no-energy-no-wage' $overtime.Text
  $null = Save-Screenshot 'failure-overtime'

  $before = Get-Frame (Get-Snapshot 'before-failure-round2')
  $null = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.Click("settle");'
  $null = Wait-Presentation $before 'failure-round2-natural-playback'
  $failed = Assert-EmptyTerminal 'Failed' 'failure-round2-terminal'
  Assert-Check ($failed.Fields.day -eq '1') 'failure.no-shop-no-day2' $failed.Fields.day
  $null = Save-Screenshot 'terminal-failure'
  $before = Get-Frame (Get-Snapshot 'before-failure-restart')
  $null = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.Click("restart");'
  $null = Wait-Frames $before 'after-failure-restart'
  $null = Assert-NewOpening 'restart-after-failure'
  $null = Save-Screenshot 'restart-after-failure'

  $screenshots = Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.png' | ForEach-Object {
    @{ file = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
  }
  Assert-Check (($screenshots.sha256 | Select-Object -Unique).Count -eq $screenshots.Count) 'screenshots.not-duplicate-hashes' $screenshots
  $report = @{ mode = $Mode; seed = $Seed; timestamp = (Get-Date).ToString('o'); checks = $script:Checks; failures = $script:Failures; screenshots = $screenshots; visualReview = 'Required: inspect each PNG for Overlay, phase, readings, and legibility; nonempty bytes alone do not prove visual acceptance.' }
  $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'terminal-report.json') -Encoding utf8
  if ($script:Failures.Count -gt 0) { throw ($script:Failures -join "`n") }
  Write-Output "Terminal checks passed: $($script:Checks.Count). Inspect PNGs before closeout. Evidence: $OutputDirectory"
}
catch {
  @{ error = $_.ToString(); checks = $script:Checks; failures = $script:Failures } |
    ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'terminal-error.json') -Encoding utf8
  throw
}
