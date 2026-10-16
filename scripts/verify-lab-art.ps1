#Requires -Version 7.3
param([switch]$SkipNaturalRuns)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-15/lab'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
function Invoke-Editor([string]$Name,[string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $r = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$r.success) { throw ($r | ConvertTo-Json -Depth 8) }
    if ($Name -eq 'eval' -and !$r.data.result.success) { throw ($r.data.result | ConvertTo-Json -Depth 8) }
    return $r.data.result
}
function Eval([string]$Code) { (Invoke-Editor 'eval' @($Code,'30000')).result }
function Check([bool]$condition,[string]$name) {
    if (!$condition) { throw "FAIL $name" }
    $checks.Add(@{check=$name;result='PASS'}); Write-Output "PASS $name"
}
function Step([string]$code) { $result=Eval $code; Check ($result -match 'PASS') $result }
function Frames([int]$count=20) {
    $frame = Eval 'return UnityEngine.Time.frameCount;'
    Invoke-Editor 'wait_for' @('--condition',"{`"member`":`"UnityEngine.Time.frameCount`",`"op`":`"greaterThan`",`"value`":$($frame+$count)}",'--timeout_s','10') | Out-Null
}
function Capture([string]$name) {
    $r=Invoke-Editor 'capture_game_view' @('--source','screen','--width','1920','--height','1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$name.png"),[Convert]::FromBase64String($r.base64))
}
function Unlock([string]$name) {
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Frames 2
    Step "return TheCall.Editor.LabArtAcceptance.SelectResearch(`"$name`");"
    Frames 2
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Step "return TheCall.Editor.LabArtAcceptance.CheckUnlocked(`"$name`");"
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("BackButton");'
    Frames 2
}
function Wait-Playback {
    $deadline=(Get-Date).AddMinutes(4)
    while (Eval 'return TheCall.Scoring.ScoringShow.IsPlaying;') {
        Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.Scoring.ScoringShow>()?.Advance();' | Out-Null
        Frames 20
        if ((Get-Date) -gt $deadline) { throw 'Playback timeout' }
    }
    Frames
}
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode first.' }
Step 'return TheCall.Editor.LabArtIntegration.ValidateSaved();'
$hash=(Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$console=Invoke-Editor 'console' @('--tail','1')
$background=Eval 'return UnityEngine.Application.runInBackground;'
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    Frames
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("capacity",8,"docs/audit/2026-10-15/lab/fixtures");'
    Step 'return TheCall.Editor.LabArtAcceptance.CheckLayout();'
    Capture '01-locked-room'
    Step 'return TheCall.Editor.LabArtAcceptance.BeginCarry();'
    Capture '02-enlarged-drag'
    Step 'return TheCall.Editor.LabArtAcceptance.FinishCarry();'
    Unlock '槽位扩容'
    Frames 30
    Step 'return TheCall.Editor.LabArtAcceptance.CheckLayout();'
    Unlock '大乱炖'
    Frames 30
    Step 'return TheCall.Editor.LabArtAcceptance.CheckLayout();'
    Unlock '基因实验'
    Unlock '基因仓储'
    Step 'return TheCall.Editor.LabArtAcceptance.PlaceFirstParent();'
    Capture '03-expanded-lab'
    Step 'return TheCall.Editor.LabArtAcceptance.HoverRecycler(true);'
    Frames 30
    Step 'return TheCall.Editor.LabArtAcceptance.CheckMouth(true);'
    Capture '04-recycler-open'
    Step 'return TheCall.Editor.LabArtAcceptance.Discard();'
    Frames 12
    Step 'return TheCall.Editor.LabArtAcceptance.CheckMouth(false);'
    Capture '05-recycled-gene'
    Step 'return TheCall.Editor.LabArtAcceptance.HoverRecycler(false);'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Frames 2
    Step 'return TheCall.Editor.LabArtAcceptance.CheckResearchText();'
    Capture '06-research'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("BackButton");'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("inventory",30,"docs/audit/2026-10-15/lab/fixtures");'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(30);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(29,0);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(0);'
    Capture '07-large-cage'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Begin("capacity",6,"docs/audit/2026-10-15/lab/fixtures");'
    for($i=0;$i -lt 6;$i++) { Step "return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(0,$i);" }
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("NextDayButton");'
    Invoke-Editor 'wait_for' @('--condition','{"findType":"TheCall.LabSceneView","member":"EnergyArrivals","op":"greaterThan","value":0}','--timeout_s','30') | Out-Null
    Step 'return TheCall.Editor.LabArtAcceptance.CheckEnergy();'
    Capture '08-energy-collection'
    Wait-Playback
    Step 'return TheCall.Editor.LabArtAcceptance.Reset();'
    if (!$SkipNaturalRuns) {
        $states=[Collections.Generic.List[string]]::new(); $logs=[Collections.Generic.List[string]]::new(); $traces=[Collections.Generic.List[string]]::new()
        $plan=(Join-Path $project 'docs/audit/2026-10-12/remediation/evidence/natural-plan.json').Replace('\','/')
        for($mode=0;$mode -le 2;$mode++) {
            $dir=(Join-Path $evidence "natural-$mode").Replace('\','/')
            Eval "return TheCall.Editor.M5PlayModeAutomation.BeginPlan(`"$plan`",$mode,`"$dir`");" | Out-Null
            $deadline=(Get-Date).AddMinutes(18); $last=''
            do {
                Start-Sleep -Seconds 5
                $progress=Eval 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus;'
                if($progress -ne $last) { Write-Output "mode=$mode $progress"; $last=$progress }
                if($progress -notmatch '^running|^completed') { throw $progress }
                if((Get-Date) -gt $deadline) { throw "Natural run $mode timeout" }
            } while($progress -match '^running')
            Check ($progress -eq 'completed 125') "Seven-day player replay mode $mode completes 125 inputs"
            Capture "victory-$mode"
            $states.Add((Get-Content (Join-Path $dir 'final-state.txt') -Raw))
            $logs.Add((Get-Content (Join-Path $dir 'production-log.txt') -Raw))
            $traces.Add((Get-Content (Join-Path $dir 'state-trace.txt') -Raw))
        }
        Check ($states[0] -ceq $states[1] -and $states[0] -ceq $states[2]) 'Normal/fast/skip final states match'
        Check ($logs[0] -ceq $logs[1] -and $logs[0] -ceq $logs[2]) 'Normal/fast/skip production records match'
        Check ($traces[0] -ceq $traces[1] -and $traces[0] -ceq $traces[2]) 'Normal/fast/skip every input state matches'
        $previous=Join-Path $project 'docs/audit/2026-10-13/architecture/natural-0/final-state.txt'
        if(Test-Path $previous) {
            Check ($states[0] -ceq (Get-Content $previous -Raw)) 'Final state matches pre-art acceptance baseline'
            $baselineDirectory=Split-Path $previous -Parent
            Check ($logs[0] -ceq (Get-Content (Join-Path $baselineDirectory 'production-log.txt') -Raw)) 'Production records match pre-art acceptance baseline'
            Check ($traces[0] -ceq (Get-Content (Join-Path $baselineDirectory 'state-trace.txt') -Raw)) 'Every input state matches pre-art acceptance baseline'
        }
    }
    $errors=Invoke-Editor 'console' @('--level','error','--since',[string]$console.cursor,'--since_session',[string]$console.session)
    $errors | ConvertTo-Json -Depth 9 | Set-Content (Join-Path $evidence 'console.json') -Encoding utf8
    Check ($errors.entries.Count -eq 0 -and !$errors.groundTruth.compilationFailed) 'No new Unity errors'
    Check ($hash -eq (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash) 'Acceptance leaves saved scene unchanged'
    $suffix=if($SkipNaturalRuns){'-controls'}else{''}
    $checks | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence "acceptance$suffix.json") -Encoding utf8
} finally {
    Invoke-Editor 'editor_stop' | Out-Null
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    Eval "UnityEngine.Application.runInBackground=$($background.ToString().ToLowerInvariant());" | Out-Null
}
