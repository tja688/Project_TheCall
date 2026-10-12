param([int]$PlaybackMode = 0)
$projectRoot = 'C:/Users/jinji/Documents/GitHub/Project_TheCall'
$planPath = "$projectRoot/docs/audit/2026-10-12/remediation/evidence/natural-plan.json"
function Invoke-EditorEval([string]$Code) {
  $result = & unity --no-banner --non-interactive command eval --project-path $projectRoot --caller plugin --skill unity-cli --format json -- $Code 30000 | ConvertFrom-Json
  if (-not $result.success -or -not $result.data.result.success) { throw ($result | ConvertTo-Json -Depth 9) }
  return $result.data.result.result
}
$begin = Invoke-EditorEval "return TheCall.Editor.M5PlayModeAutomation.BeginPlan(`"$planPath`", $PlaybackMode);"
Write-Output $begin
$deadline = (Get-Date).AddMinutes(20)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 10
  $progress = Invoke-EditorEval 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus + " frame=" + UnityEngine.Time.frameCount;'
  Write-Output $progress
  if ($progress -match '^completed 125') { exit 0 }
  if ($progress -notmatch '^running') { throw "Player replay stopped: $progress" }
}
throw 'Player replay timed out'
