#Requires -Version 7.3
param([switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-17/facility'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
function Invoke-Editor([string]$Name,[string[]]$Arguments=@()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw ($response | ConvertTo-Json -Depth 8) }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$code) { (Invoke-Editor 'eval' @($code,'30000')).result }
function Step([string]$code) {
    $result = Eval $code
    if ($result -notmatch 'PASS') { throw $result }
    $checks.Add(@{check=$result; result='PASS'})
    Write-Output $result
}
function Wait-Member([string]$type,[string]$member,[string]$op,[object]$value) {
    $condition = @{findType=$type;member=$member;op=$op;value=$value} | ConvertTo-Json -Compress
    $result = Invoke-Editor 'wait_for' @('--condition',$condition,'--timeout_s','12')
    if (!$result.met) { throw "Wait timed out: $condition" }
}
function Seconds([double]$duration) {
    $until = [double](Eval 'return UnityEngine.Time.realtimeSinceStartupAsDouble;') + $duration
    $condition = @{member='UnityEngine.Time.realtimeSinceStartupAsDouble';op='greaterThan';value=$until} | ConvertTo-Json -Compress
    $result = Invoke-Editor 'wait_for' @('--condition',$condition,'--timeout_s','12')
    if (!$result.met) { throw 'Player time did not advance' }
}
function Capture([string]$name) {
    Eval 'foreach(var h in UnityEngine.Object.FindObjectsByType<TheCall.MonsterHover>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {h.HideManual();h.enabled=false;}' | Out-Null
    $result = Invoke-Editor 'capture_game_view' @('--source','screen','--width','1920','--height','1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$name.png"),[Convert]::FromBase64String($result.base64))
    Eval 'foreach(var h in UnityEngine.Object.FindObjectsByType<TheCall.MonsterHover>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) h.enabled=true;' | Out-Null
}
function Unlock([string]$name) {
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Step "return TheCall.Editor.LabArtAcceptance.SelectResearch(`"$name`");"
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");'
    Step "return TheCall.Editor.LabArtAcceptance.CheckUnlocked(`"$name`");"
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("BackButton");'
    Seconds 0.9
}
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode first.' }
Step 'return TheCall.Editor.FacilityArtAcceptance.Saved();'
$savedHash = (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
Invoke-Editor 'clear_console' | Out-Null
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    $moving = Invoke-Editor 'wait_for' @('--condition','{"member":"UnityEngine.Time.frameCount","op":"changed"}','--timeout_s','10')
    if (!$moving.met) { throw 'Player is frozen' }
    Step 'return TheCall.Editor.FacilityArtAcceptance.Fixture(14);'
    Step 'return TheCall.Editor.FacilityArtAcceptance.PanBegin();'
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.PanCheck(false);'
    Capture '01-dorm-monitor'
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(true);'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $true
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossFinish(true);'
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(false);'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $false
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossFinish(false);'
    Step 'return TheCall.Editor.FacilityArtAcceptance.CrossBegin(true);'
    Wait-Member 'TheCall.FacilityPanorama' 'Production' 'equals' $true
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.RejectCrossDrop();'
    Seconds 1.1
    Step 'return TheCall.Editor.FacilityArtAcceptance.RejectedCheck();'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityDormTab");'
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityDormNext");'
    Step 'return TheCall.Editor.FacilityArtAcceptance.PreviewUpgrades(true);'
    Capture '02-dorm-upgrade-art-preview'
    Step 'return TheCall.Editor.FacilityArtAcceptance.PreviewUpgrades(false);'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityProductionTab");'
    Wait-Member 'TheCall.FacilityPanorama' 'Moving' 'equals' $false
    Step 'return TheCall.Editor.FacilityArtAcceptance.FillProduction();'
    Unlock '槽位扩容'
    Unlock '大乱炖'
    Step 'return TheCall.Editor.FacilityArtAcceptance.Expanded();'
    Step 'return TheCall.Editor.FacilityArtAcceptance.UpgradeBindings();'
    Capture '03-production-expanded'
    Step 'return TheCall.Editor.FacilityArtAcceptance.Lamps(false);'
    Capture 'lighting-off'
    Step 'return TheCall.Editor.FacilityArtAcceptance.Lamps(true);'
    Step 'return TheCall.Editor.FacilityArtAcceptance.RecyclerHover();'
    Wait-Member 'TheCall.LabRecycler' 'Openness' 'greaterThan' 0.95
    Step 'return TheCall.Editor.FacilityArtAcceptance.RecyclerCheck();'
    Capture '04-recycler-open'
    Step 'return TheCall.Editor.FacilityArtAcceptance.Recycle();'
    Seconds 0.9
    Capture '05-recycled-gene'
    Step 'return TheCall.Editor.FacilityArtAcceptance.Fixture(31);'
    while (Eval 'return ((TheCall.LabCageLift)UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().lab.cageScroll).nextButton.interactable;') {
        Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible(((TheCall.LabCageLift)UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().lab.cageScroll).nextButton.name);'
        Seconds 0.6
    }
    Step 'return TheCall.Editor.FacilityArtAcceptance.TailDrop();'
    Capture 'cage-tail'
    Step 'return TheCall.Editor.FacilityArtAcceptance.PrepareShop();'
    $deadline = (Get-Date).AddMinutes(3)
    while (Eval 'return TheCall.Scoring.ScoringShow.IsPlaying;') {
        Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.Scoring.ScoringShow>()?.Advance();' | Out-Null
        Seconds 0.3
        if ((Get-Date) -gt $deadline) { throw 'Settlement presentation timed out' }
    }
    Seconds 0.2
    Step 'return TheCall.Editor.FacilityArtAcceptance.ShopCheck();'
    Capture '06-shop-monsters'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityToolCategory");'
    Seconds 0.15
    Step 'return TheCall.Editor.FacilityArtAcceptance.BuyTool();'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("FacilityDoctorTalk");'
    Capture '07-shop-tools'
    Step 'return TheCall.Editor.FacilityArtAcceptance.RefreshShop();'
    Step 'return TheCall.Editor.HandbookAcceptance.ClickVisible("SellTab");'
    Seconds 0.15
    Step 'return TheCall.Editor.FacilityArtAcceptance.SellMonster();'
    Capture '08-shop-selling'
    $console = Invoke-Editor 'console' @('--tail','50','--level','error')
    $console | ConvertTo-Json -Depth 9 | Set-Content (Join-Path $evidence 'console.json') -Encoding utf8
    if ($console.entries.Count -gt 0) { throw 'Unity console contains runtime errors' }
    $checks.Add(@{check='No runtime Unity errors'; result='PASS'})
    @{kind='LOCAL_UI_FIXTURES_NOT_NATURAL_RUN'; checks=$checks; count=$checks.Count; savedSceneHash=$savedHash} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'acceptance.json') -Encoding utf8
} finally {
    if (!$KeepPlaying) {
        Invoke-Editor 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    }
}
if (!$KeepPlaying -and (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash -ne $savedHash) { throw 'Runtime verification modified saved Main scene' }
