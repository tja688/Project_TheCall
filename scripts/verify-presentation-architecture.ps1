#Requires -Version 7.3
param([switch]$SkipNaturalRuns, [switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-13/architecture'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()

function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw "$Name $($Arguments[0]): $($response | ConvertTo-Json -Depth 8)" }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code) { return (Invoke-Editor 'eval' @($Code, '30000')).result }
function Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL: $Name" }
    $checks.Add(@{ check = $Name; result = 'PASS' })
    Write-Output "PASS $Name"
}
function Capture([string]$Name) {
    $capture = Invoke-Editor 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$Name.png"), [Convert]::FromBase64String($capture.base64))
}
function Wait-Playback([bool]$ClickToSkip = $true) {
    $deadline = (Get-Date).AddMinutes(4)
    while ((Get-Date) -lt $deadline) {
        if (!(Eval 'return TheCall.Scoring.ScoringShow.IsPlaying;')) {
            # Completion deliberately refreshes on the following frame.
            Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10') | Out-Null
            return
        }
        if ($ClickToSkip) {
            Eval 'var op=UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>(UnityEngine.FindObjectsInactive.Include); var blocker=op!=null ? op.transform.Find("ScoringBlocker") : null; if(blocker!=null && blocker.gameObject.activeInHierarchy) UnityEngine.EventSystems.ExecuteEvents.Execute(blocker.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);' | Out-Null
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'Playback did not complete.'
}
function Start-EmptyOperation {
    Eval 'var p=UnityEngine.Object.FindAnyObjectByType<TheCall.TheCallPresentation>(); p.Perform(TheCall.PresentationAction.ReturnToTitle); var opening=UnityEngine.Object.FindAnyObjectByType<TheCall.OpeningScreenView>(); opening.candidates[0].button.onClick.Invoke();' | Out-Null
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery())==TheCall.RunPhase.Operation;') 'Player opening choice reaches operation'
}

$status = (& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json) | ConvertFrom-Json
if (!$status.success -or $status.data.count -ne 1) { throw 'One ready Editor on this project is required.' }
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before starting this full acceptance.' }
$sceneHash = (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$baseline = Invoke-Editor 'console_status'
Invoke-Editor 'set_autotick' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    Check ((Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10')).met) 'Player frames advance'
    Eval 'TheCall.Editor.PresentationArchitectureAcceptance.PrepareButtonAdapter();' | Out-Null
    Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10') | Out-Null
    foreach ($method in @('CheckButtonAdapter', 'CheckLegacyButtonAdapter', 'CheckControllerReenable')) {
        Check ((Eval "return TheCall.Editor.PresentationArchitectureAcceptance.$method();") -like 'PASS*') $method
    }
    Check ((Eval 'return TheCall.Editor.HandbookAcceptance.Run();') -like 'PASS*') 'Existing handbook/settings/log bindings and read-only browsing'
    Capture 'opening'
    Start-EmptyOperation
    Check ((Eval 'return TheCall.Editor.PresentationArchitectureAcceptance.CheckOptionalOperationControls();') -like 'PASS*') 'Optional labels/buttons can be removed'
    Capture 'operation'
    Check ((Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().nextDayButton.onClick.Invoke(); return TheCall.Editor.PresentationArchitectureAcceptance.CheckRefreshDuringPlayback();') -like 'PASS*') 'Refresh during actual settlement'
    Wait-Playback
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.LevelTargetQuery()).InOvertime;') 'Empty extraction enters overtime naturally'
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().nextDayButton.onClick.Invoke();' | Out-Null
    Wait-Playback
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery())==TheCall.RunPhase.Failed && UnityEngine.Object.FindAnyObjectByType<TheCall.ResultScreenView>().gameObject.activeInHierarchy;') 'Second insufficient settlement reaches failure screen'
    Capture 'failure'
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.ResultScreenView>().restartButton.onClick.Invoke();' | Out-Null
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery())==TheCall.RunPhase.Opening;') 'Original restart button reopens a new run'
    Start-EmptyOperation
    Check ((Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().nextDayButton.onClick.Invoke(); return TheCall.Editor.PresentationArchitectureAcceptance.CheckResetDuringPlayback();') -like 'PASS*') 'Reset during actual playback'
    Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10') | Out-Null
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery())==TheCall.RunPhase.Opening && !TheCall.Scoring.ScoringShow.IsPlaying;') 'Old completion cannot revive the prior run'
    Start-EmptyOperation
    Check ((Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().nextDayButton.onClick.Invoke(); return TheCall.Editor.PresentationArchitectureAcceptance.CheckOwnerDisableDuringPlayback();') -like 'PASS*') 'Owner disable and reenable during actual playback'

    if (!$SkipNaturalRuns) {
        $plan = (Join-Path $project 'docs/audit/2026-10-12/remediation/evidence/natural-plan.json').Replace('\', '/')
        $finalStates = [System.Collections.Generic.List[string]]::new()
        $logs = [System.Collections.Generic.List[string]]::new()
        $stateTraces = [System.Collections.Generic.List[string]]::new()
        for ($mode = 0; $mode -le 2; $mode++) {
            $directory = (Join-Path $evidence "natural-$mode").Replace('\', '/')
            Eval "return TheCall.Editor.M5PlayModeAutomation.BeginPlan(`"$plan`",$mode,`"$directory`");" | Out-Null
            $deadline = (Get-Date).AddMinutes(18)
            $lastProgress = ''
            do {
                Start-Sleep -Seconds 5
                $progress = Eval 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus;'
                if ($progress -ne $lastProgress) { Write-Output "mode=$mode $progress"; $lastProgress = $progress }
                if ($progress -notmatch '^running|^completed') { throw $progress }
                if ((Get-Date) -gt $deadline) { throw "Natural run $mode timed out at $progress" }
            } while ($progress -match '^running')
            Check ($progress -eq 'completed 125') "Natural seven-day player run mode $mode completes 125 inputs"
            Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery())==TheCall.RunPhase.Victory && UnityEngine.Object.FindAnyObjectByType<TheCall.ResultScreenView>().gameObject.activeInHierarchy;') "Actual victory screen mode $mode"
            Capture "victory-$mode"
            $finalStates.Add((Get-Content (Join-Path $directory 'final-state.txt') -Raw))
            $logs.Add((Get-Content (Join-Path $directory 'production-log.txt') -Raw))
            $stateTraces.Add((Get-Content (Join-Path $directory 'state-trace.txt') -Raw))
        }
        Check ($finalStates[0] -ceq $finalStates[1] -and $finalStates[0] -ceq $finalStates[2]) 'Normal/fast/skip final rule state is identical'
        Check ($logs[0] -ceq $logs[1] -and $logs[0] -ceq $logs[2]) 'Normal/fast/skip production log is identical'
        Check ($stateTraces[0] -ceq $stateTraces[1] -and $stateTraces[0] -ceq $stateTraces[2]) 'Normal/fast/skip rule state after every input is identical'
    }
    $console = Invoke-Editor 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
    Check ($console.entries.Count -eq 0 -and !$console.groundTruth.compilationFailed) 'No new Unity errors or exceptions'
    Check ($sceneHash -eq (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash) 'Saved scene remains unchanged by acceptance'
    $suffix = if ($SkipNaturalRuns) { '-controls-final' } else { '' }
    $checks | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence "acceptance$suffix.json") -Encoding utf8
    $console | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $evidence "console$suffix.json") -Encoding utf8
    Write-Output "PASS. Evidence: $evidence"
}
finally {
    if (!$KeepPlaying) {
        Invoke-Editor 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    }
}
