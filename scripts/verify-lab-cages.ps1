#Requires -Version 7.3
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-16/cages'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
function Cmd([string]$name,[string[]]$arguments=@()) {
    $raw = & unity --no-banner --non-interactive command $name --project-path $project --caller plugin --skill unity-cli --format json -- @arguments
    $r = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$r.success -or ($name -eq 'eval' -and !$r.data.result.success)) { throw ($r|ConvertTo-Json -Depth 8) }
    $r.data.result
}
function Eval([string]$code) { (Cmd 'eval' @($code,'120000')).result }
function Step([string]$code) {
    $r=Eval $code
    if ($r -notmatch 'PASS') { throw $r }
    $checks.Add(@{check=$r;result='PASS'}); Write-Host $r
}
function Frames([int]$count=5) {
    $frame=Eval 'return UnityEngine.Time.frameCount;'
    $r=Cmd 'wait_for' @('--condition',"{`"member`":`"UnityEngine.Time.frameCount`",`"op`":`"greaterThan`",`"value`":$($frame+$count)}",'--timeout_s','10')
    if (!$r.met) { throw 'Game loop is not advancing' }
}
function Capture([string]$name) {
    $r=Cmd 'capture_game_view' @('--source','screen','--width','1920','--height','1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$name.png"),[Convert]::FromBase64String($r.base64))
}
function Motion([string]$action) {
    Step "return TheCall.Editor.LabCageAcceptance.BeginMotion(`"$action`",`"docs/audit/2026-10-16/cages/motion`");"
    Cmd 'wait_for' @('--condition','{"member":"TheCall.Editor.LabCageAcceptance.MotionStatus","op":"contains","value":"PASS"}','--timeout_s','10') | Out-Null
    Step 'return TheCall.Editor.LabCageAcceptance.MotionStatus;'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.CheckHits();'
}
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode first.' }
Step 'return TheCall.Editor.LabCageRefinement.Apply();'
$objects=Eval 'return UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Length;'
Step 'return TheCall.Editor.LabCageRefinement.Apply();'
$again=Eval 'return UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Length;'
if($objects -ne $again){throw 'Repeated cage authoring duplicated scene objects'}
$checks.Add(@{check="Idempotent authoring: $objects objects after both passes";result='PASS'})
$hash=(Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$background=Eval 'return UnityEngine.Application.runInBackground;'
$console=Cmd 'console' @('--tail','1','--level','error')
Cmd 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Cmd 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    Frames
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory",8,"docs/audit/2026-10-16/cages/fixtures");'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(8);'
    Step 'return TheCall.Editor.LabCageAcceptance.CheckHits();'
    Capture '01-first-four'
    Motion 'down'
    Capture '02-next-four'
    Motion 'up'
    Motion 'wheel-down'
    Capture '03-wheel-row'
    Motion 'wheel-up'
    Step 'return TheCall.Editor.LabArtAcceptance.BeginCarry();'
    Capture '04-drag-outside-mask'
    Step 'return TheCall.Editor.LabArtAcceptance.FinishCarry();'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(0);'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(8);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory",31,"docs/audit/2026-10-16/cages/fixtures");'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(31);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(31);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(30,0);'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(30);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(0);'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(31);'
    Step 'return TheCall.Editor.LabCageAcceptance.CheckHits();'
    Capture '05-large-inventory-last-row'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory",1,"docs/audit/2026-10-16/cages/fixtures");'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(1);'
    Step 'return TheCall.Editor.LabCageAcceptance.CheckHits();'
    Capture '06-single-occupant'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(0,0);'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.Check(0);'
    Capture '07-empty-cabinet'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory",8,"docs/audit/2026-10-16/cages/fixtures");'
    Motion 'down'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("BackButton");'
    Frames
    Step 'return TheCall.Editor.LabCageAcceptance.CheckReset();'
    Step 'return TheCall.Editor.LabCageAcceptance.Check(8);'
    Capture '08-final'
    $errors=Cmd 'console' @('--tail','50','--level','error','--since',"$($console.cursor)")
    $errors|ConvertTo-Json -Depth 8|Set-Content (Join-Path $evidence 'console.json') -Encoding utf8
    if($errors.entries.Count){throw 'New Unity errors during cage verification'}
    $checks.Add(@{check='No new Unity errors during runtime verification';result='PASS'})
} finally {
    Cmd 'editor_stop' | Out-Null
    Eval "UnityEngine.Application.runInBackground=$($background.ToString().ToLowerInvariant());" | Out-Null
    $checks|ConvertTo-Json -Depth 5|Set-Content (Join-Path $evidence 'acceptance.json') -Encoding utf8
}
if ((Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash -ne $hash) { throw 'Play Mode changed saved scene' }
Write-Host "PASS $($checks.Count) checks; Play Mode left the saved scene unchanged"
