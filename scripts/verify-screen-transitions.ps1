#Requires -Version 7.3
param([switch]$SkipNaturalRuns, [switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-20/transitions'
$recording = Join-Path $project 'Temp/ScreenTransitions'
New-Item -ItemType Directory -Path $evidence,$recording -Force | Out-Null
function Invoke-Editor([string]$Name,[string[]]$Arguments=@()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw ($response | ConvertTo-Json -Depth 8) }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    $response.data.result
}
function Eval([string]$Code) { (Invoke-Editor 'eval' @($Code,'30000')).result }
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before verification.' }
$saved = Eval 'return TheCall.Editor.ScreenTransitionAcceptance.Saved();'
Write-Output $saved
$sceneHash = (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$background = Eval 'return UnityEngine.Application.runInBackground;'
Invoke-Editor 'clear_console' | Out-Null
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    $frames = Invoke-Editor 'wait_for' @('--condition','{"member":"UnityEngine.Time.frameCount","op":"changed"}','--timeout_s','10')
    if (!$frames.met) { throw 'Player frame count is frozen.' }
    Eval "return TheCall.Editor.ScreenTransitionAcceptance.Begin(`"$($evidence.Replace('\','/'))`");" | Out-Null
    $deadline = (Get-Date).AddMinutes(4)
    do {
        Start-Sleep -Seconds 3
        $progress = Eval 'return TheCall.Editor.ScreenTransitionAcceptance.Status;'
        Write-Output $progress
        if ($progress -notmatch '^running|^completed') { throw $progress }
        if ((Get-Date) -gt $deadline) { throw 'Transition acceptance timed out.' }
    } while ($progress -match '^running')
    if (!$SkipNaturalRuns) {
        $plan = (Join-Path $project 'docs/audit/2026-10-12/remediation/evidence/natural-plan.json').Replace('\','/')
        foreach ($mode in 0,1,2) {
            $directory = (Join-Path $recording "natural-$mode").Replace('\','/')
            Eval "return TheCall.Editor.M5PlayModeAutomation.BeginPlan(`"$plan`",$mode,`"$directory`");" | Out-Null
            $deadline = (Get-Date).AddMinutes(20)
            do {
                Start-Sleep -Seconds 5
                $progress = Eval 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus;'
                Write-Output "mode=$mode $progress"
                if ($progress -notmatch '^running|^completed') { throw $progress }
                if ((Get-Date) -gt $deadline) { throw 'Natural replay timed out.' }
            } while ($progress -match '^running')
            if ($progress -ne 'completed 125') { throw "Wrong input count: $progress" }
            $published = Join-Path $evidence "natural-$mode"
            New-Item -ItemType Directory -Path $published -Force | Out-Null
            Get-ChildItem -LiteralPath $directory -File | Copy-Item -Destination $published -Force
        }
        $comparisons = foreach ($mode in 1,2) {
            foreach ($file in 'final-state.txt','production-log.txt','state-trace.txt') {
                $actual = Join-Path $evidence "natural-$mode/$file"
                $baseline = Join-Path $evidence "natural-0/$file"
                $equal = [IO.File]::ReadAllText($actual) -ceq [IO.File]::ReadAllText($baseline)
                if (!$equal) { throw "Playback modes differ: $mode/$file" }
                @{mode=$mode; file=$file; matches=$equal; sha256=(Get-FileHash $actual).Hash}
            }
        }
        $comparisons | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'replay-comparison.json') -Encoding utf8
    }
    $console = Invoke-Editor 'console' @('--tail','50','--level','error')
    $console | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'console.json') -Encoding utf8
    if ($console.entries.Count -gt 0) { throw 'Runtime Unity errors occurred.' }
    Write-Output 'PASS screen transitions and runtime console'
} finally {
    if (!$KeepPlaying) {
        Invoke-Editor 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
        Eval "UnityEngine.Application.runInBackground=$($background.ToString().ToLowerInvariant());" | Out-Null
    }
}
if (!$KeepPlaying -and (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash -ne $sceneHash) {
    throw 'Play Mode verification modified the saved Main scene.'
}
