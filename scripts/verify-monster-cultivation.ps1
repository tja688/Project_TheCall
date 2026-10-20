#Requires -Version 7.3
param([string]$EvidenceDirectory = 'docs/audit/2026-10-20/templates/cultivation')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = if ([IO.Path]::IsPathRooted($EvidenceDirectory)) { $EvidenceDirectory } else { Join-Path $project $EvidenceDirectory }
$evidence = [IO.Path]::GetFullPath($evidence)
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
$failures = [Collections.Generic.List[string]]::new()
$started = $false
$sceneUnchanged = $false

function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw "$Name failed: $($response | ConvertTo-Json -Depth 8)" }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code) { (Invoke-Editor 'eval' @($Code, '30000')).result }
function NextFrame {
    $result = Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '15')
    if (!$result.met) { throw 'Play Mode did not render another frame' }
}
function Wait-Shop {
    $condition = '{"member":"TheCall.Editor.MonsterCultivationAcceptance.ReadyForCheck","op":"equals","value":true}'
    $result = Invoke-Editor 'wait_for' @('--condition', $condition, '--timeout_s', '45')
    if (!$result.met) { throw "Actual shop did not become ready: $(Eval 'return TheCall.Editor.MonsterCultivationAcceptance.Stage;')" }
}
function Wait-Stage([string]$Member) {
    $condition = '{"member":"TheCall.Editor.MonsterCultivationAcceptance.' + $Member + '","op":"equals","value":true}'
    $result = Invoke-Editor 'wait_for' @('--condition', $condition, '--timeout_s', '45')
    if (!$result.met) { throw "UI stage $Member not ready: $(Eval 'return TheCall.Editor.MonsterCultivationAcceptance.Stage;')" }
}
function Enter-PendingDay {
    Wait-Stage 'ReadyForDayStart'
    Step 'real begin-day after transition' 'return TheCall.Editor.MonsterCultivationAcceptance.BeginPendingDay();'
    Wait-Stage 'ReadyForOperation'
}
function Step([string]$Name, [string]$Code) {
    $result = Eval $Code
    if ($result -notlike 'PASS*') { throw "$Name has no PASS verdict: $result" }
    $checks.Add([ordered]@{ name = $Name; result = $result; kind = 'LOCAL_NOT_NATURAL' })
    Write-Output $result
}
function Capture([string]$Name) {
    Eval 'foreach(var hover in UnityEngine.Object.FindObjectsByType<TheCall.MonsterHover>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) { hover.HideManual(); hover.enabled=false; }' | Out-Null
    try {
        NextFrame
        $capture = Invoke-Editor 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
        [IO.File]::WriteAllBytes((Join-Path $evidence "$Name.png"), [Convert]::FromBase64String($capture.base64))
    } finally {
        Eval 'foreach(var hover in UnityEngine.Object.FindObjectsByType<TheCall.MonsterHover>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) hover.enabled=true;' | Out-Null
    }
}
function Wait-Editor {
    $raw = & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json
    if ($LASTEXITCODE -ne 0) { throw "Editor did not become ready: $($raw -join "`n")" }
}

# Existing project instance only. No duplicate Editor, scene rebuild or saved-scene edit.
$status = & unity --no-banner --non-interactive status --project-path $project --format json
if ($LASTEXITCODE -ne 0) { throw "Cannot inspect this project's Editor: $($status -join "`n")" }
Wait-Editor
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before running the cultivation fixtures.' }
if (!(Eval 'var s=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); return s.path=="Assets/Scenes/Main.unity" && !s.isDirty;')) {
    throw 'Open and save the Main scene before running cultivation fixtures.'
}
$savedHash = (Get-FileHash -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$baseline = Invoke-Editor 'console_status'
$evidenceCode = $evidence.Replace('\', '/').Replace('"', '\"')
try {
    Invoke-Editor 'set_autotick' | Out-Null
    Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
    $started = $true
    Invoke-Editor 'editor_play' | Out-Null
    Wait-Editor
    NextFrame

    Step 'install before' "return TheCall.Editor.MonsterCultivationAcceptance.InstallBegin(`"$evidenceCode`");"
    Capture '01-install-before'
    Step 'real gene install' 'return TheCall.Editor.MonsterCultivationAcceptance.InstallGene();'
    Capture '02-install-after-unchanged-appearance'

    Step 'real parent and worker placements' "return TheCall.Editor.MonsterCultivationAcceptance.BreedingBegin(`"$evidenceCode`",false);"
    Capture '03-breeding-parent-layout'
    Step 'real breeding-day settlement' 'return TheCall.Editor.MonsterCultivationAcceptance.BreedingSettle();'
    Wait-Shop
    Capture '04-breeding-day-shop'
    Step 'real child birth' 'return TheCall.Editor.MonsterCultivationAcceptance.BreedingCheck();'
    Enter-PendingDay
    Step 'real next-day child portrait' 'return TheCall.Editor.MonsterCultivationAcceptance.BreedingNextDay();'
    Capture '05-inherited-child-next-day'

    foreach ($day in 1, 2) {
        Step "parent work day $day begin" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodDayBegin();'
        Wait-Shop
        Step "parent work leave shop" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodLeaveShop();'
        Enter-PendingDay
        Step "parent work day $day check" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodDayCheck();'
        Capture $(if ($day -eq 1) { '06-parent-work-day-one' } else { '07-parent-tired-after-two-work-days' })
    }
    foreach ($day in 1, 2) {
        Step "parent rest day $day begin" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodDayBegin(false);'
        Wait-Shop
        Step "parent rest leave shop" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodLeaveShop();'
        Enter-PendingDay
        Step "parent rest day $day check" 'return TheCall.Editor.MonsterCultivationAcceptance.MoodDayCheck();'
        Capture $(if ($day -eq 1) { '08-parent-rest-day-one' } else { '09-parent-enthusiastic-after-two-rest-days' })
    }

    foreach ($scenario in 'install', 'breeding', 'mood') {
        $report = Get-Content -LiteralPath (Join-Path $evidence "$scenario-report.json") -Raw | ConvertFrom-Json
        if (!$report.completed -or $report.failure -or $report.kind -ne 'LOCAL_CULTIVATION_UI_FIXTURE_NOT_NATURAL_RUN') {
            throw "$scenario report did not complete cleanly as a local fixture"
        }
    }
    $moodReport = Get-Content -LiteralPath (Join-Path $evidence 'mood-report.json') -Raw | ConvertFrom-Json
    if (!($moodReport.steps | Where-Object { $_.step -eq 'after-real-work-2' -and $_.mood -eq '疲倦' }) -or
        !($moodReport.steps | Where-Object { $_.step -eq 'after-real-rest-2' -and $_.mood -eq '热情' })) {
        throw 'Actual two-work-day and two-rest-day mood observations are missing'
    }
} catch {
    $failures.Add($_.Exception.Message)
} finally {
    if ($started) {
        try { Invoke-Editor 'editor_stop' | Out-Null; Wait-Editor }
        catch { $failures.Add("Failed to stop Play Mode: $($_.Exception.Message)") }
    }
    try {
        $sceneUnchanged = (Get-FileHash -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')).Hash -ceq $savedHash
        if (!$sceneUnchanged) { $failures.Add('Cultivation acceptance changed the saved Main scene.') }
    } catch { $failures.Add("Cannot verify saved Main hash: $($_.Exception.Message)") }
    try {
        $console = Invoke-Editor 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
        $console | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'console.json') -Encoding utf8
        if ($console.entries.Count -ne 0 -or $console.groundTruth.compilationFailed) { $failures.Add('New Unity error or compilation failure during cultivation acceptance.') }
    } catch { $failures.Add("Cannot verify Unity console: $($_.Exception.Message)") }
    [ordered]@{
        kind = 'LOCAL_CULTIVATION_UI_FIXTURES_NOT_NATURAL_RUN'
        passed = $failures.Count -eq 0
        savedMainUnchanged = $sceneUnchanged
        savedMainSha256 = $savedHash
        checks = $checks.ToArray()
        failures = $failures.ToArray()
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'acceptance.json') -Encoding utf8
}
if ($failures.Count -gt 0) { throw ($failures -join "`n") }
Write-Output "PASS real install, breeding, two work days and two rest days; saved Main unchanged, no new Unity errors. Local fixture evidence: $evidence"
