#Requires -Version 7.3
param(
    [switch]$KeepPlaying,
    [int]$Seeds = 600,
    [string]$EvidenceDirectory = 'docs/audit/2026-10-20/templates'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = if ([IO.Path]::IsPathRooted($EvidenceDirectory)) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { [IO.Path]::GetFullPath((Join-Path $project $EvidenceDirectory)) }
$evidenceCode = $evidence.Replace('\', '/').Replace('"', '\"')
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
$captures = [Collections.Generic.List[object]]::new()
$started = $false
$passed = $false
$failure = $null
if ($Seeds -lt 600) { throw 'At least 600 seeds are required for complete matrix acceptance.' }

function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --timeout 600 --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw "$Name failed: $($response | ConvertTo-Json -Depth 8)" }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code, [int]$Timeout = 30000) { (Invoke-Editor 'eval' @($Code, [string]$Timeout)).result }
function Step([string]$Code, [int]$Timeout = 30000) {
    $result = Eval $Code $Timeout
    if ($result -notmatch '^PASS') { throw "No PASS verdict: $result" }
    $checks.Add(@{ check = [string]$result; result = 'PASS' })
    Write-Output $result
}
function Wait-Member([string]$Type, [string]$Member, [string]$Op, [object]$Value) {
    $condition = @{ findType = $Type; member = $Member; op = $Op; value = $Value } | ConvertTo-Json -Compress
    $result = Invoke-Editor 'wait_for' @('--condition', $condition, '--timeout_s', '12')
    if (!$result.met) { throw "Runtime wait timed out: $condition" }
}
function Next-Frame {
    $result = Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10')
    if (!$result.met) { throw 'Player frame count did not advance.' }
}
function Seconds([double]$Duration) {
    $until = [double](Eval 'return UnityEngine.Time.realtimeSinceStartupAsDouble;') + $Duration
    $condition = @{ member = 'UnityEngine.Time.realtimeSinceStartupAsDouble'; op = 'greaterThan'; value = $until } | ConvertTo-Json -Compress
    $result = Invoke-Editor 'wait_for' @('--condition', $condition, '--timeout_s', '12')
    if (!$result.met) { throw 'Player clock did not advance.' }
}
function Capture([string]$Name) {
    Next-Frame
    $capture = Invoke-Editor 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
    $path = Join-Path $evidence "$Name.png"
    [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($capture.base64))
    $captures.Add(@{ file = "$Name.png"; width = 1920; height = 1080; sha256 = (Get-FileHash -LiteralPath $path).Hash; source = 'UNITY_GAME_VIEW' })
}

if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before starting monster acceptance.' }
Step 'return TheCall.Editor.MonsterArtAcceptance.Saved();'
$savedHash = (Get-FileHash -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
$baseline = Invoke-Editor 'console_status'
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
try {
    Invoke-Editor 'editor_play' | Out-Null
    $started = $true
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Editor did not become ready after entering Play Mode.' }
    Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
    Next-Frame
    Step "return TheCall.Editor.MonsterArtAcceptance.Matrix(`"$evidenceCode`",$Seeds);" 300000
    Step "return TheCall.Editor.MonsterArtAcceptance.TintContracts(`"$evidenceCode`");" 300000
    Step "return TheCall.Editor.MonsterArtAcceptance.Contracts(`"$evidenceCode`");"

    foreach ($page in 0..4) {
        Step "return TheCall.Editor.MonsterArtAcceptance.Gallery($page);"
        Capture "gallery-$page"
    }
    Step "return TheCall.Editor.MonsterArtAcceptance.MotionBegin(`"$evidenceCode`");"
    foreach ($sample in 0..5) {
        Seconds 0.17
        Step 'return TheCall.Editor.MonsterArtAcceptance.MotionSample();'
        if ($sample -in 0, 2, 5) { Capture "motion-$sample" }
    }
    Step 'return TheCall.Editor.MonsterArtAcceptance.MotionCheck();'
    $motionHashes = @($captures | Where-Object { $_.file -like 'motion-*' } | ForEach-Object { $_.sha256 } | Select-Object -Unique)
    if ($motionHashes.Count -lt 3) { throw 'Captured motion frames have identical PNG checksums.' }
    $checks.Add(@{ check = 'Three captured Unity motion frames have distinct PNG SHA256 values'; result = 'PASS' })
    Step 'return TheCall.Editor.MonsterArtAcceptance.GalleryClose();'

    # This is a local injected fixture. It does not count as recorded player replay.
    Step "return TheCall.Editor.FacilityArtAcceptance.Fixture(14,`"$evidenceCode/fixtures`");"
    Step 'return TheCall.Editor.MonsterArtAcceptance.Consumers();'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityDormTab");'
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(true);'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Ghost();'
    Capture 'real-cross-room-ghost'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $true
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossFinish(true);'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Consumers();'
    Capture 'real-production-monster'
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(false);'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Ghost();'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $false
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossFinish(false);'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Consumers();'
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(true);'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Ghost();'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $true
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.RejectCrossDrop();'
    Seconds 1.1
    Step 'return TheCall.Editor.FacilityArtAcceptance.RejectedCheck();'
    Step 'return TheCall.Editor.MonsterArtAcceptance.Consumers();'
    Capture 'rejected-ghost-restored'

    $console = Invoke-Editor 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
    $console | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $evidence 'console.json') -Encoding utf8
    if ($console.entries.Count -gt 0 -or $console.groundTruth.compilationFailed) { throw 'Unity reported new compilation or runtime errors.' }
    $checks.Add(@{ check = 'No new Unity compilation or runtime errors'; result = 'PASS' })
    $passed = $true
}
catch {
    $failure = $_.Exception.Message
    throw
}
finally {
    if ($started) {
        try { Eval 'return TheCall.Editor.MonsterArtAcceptance.GalleryClose();' | Out-Null } catch { Write-Warning $_.Exception.Message }
        if (!$KeepPlaying) {
            Invoke-Editor 'editor_stop' | Out-Null
            & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
        }
    }
    $finalHash = (Get-FileHash -LiteralPath (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
    if ($finalHash -ne $savedHash) { $passed = $false; $failure = 'Runtime acceptance modified saved Main scene.' }
    @{
        kind = 'LOCAL_VISUAL_AND_POINTER_FIXTURES_NOT_NATURAL_RUN'
        passed = $passed; failure = $failure; checks = $checks; count = $checks.Count; captures = $captures
        requestedSeeds = $Seeds; tintContracts = "source-aligned semantic masks"; savedSceneHash = $savedHash; finalSceneHash = $finalHash
        recordedPlayerReplay = 'Run scripts/verify-facility-replay.ps1 with a separate EvidenceDirectory; these fixtures do not prove seven-day completion.'
    } | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $evidence 'acceptance.json') -Encoding utf8
}
if (!$passed) { throw $failure }
Write-Output "PASS monster system local visual and actual-pointer evidence: $evidence"
