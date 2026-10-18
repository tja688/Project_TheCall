#Requires -Version 7.3
param([switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-18/settlement'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw ($response | ConvertTo-Json -Depth 8) }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code) { (Invoke-Editor 'eval' @($Code, '30000')).result }
function Capture([string]$Name) {
    $result = Invoke-Editor 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$Name.png"), [Convert]::FromBase64String($result.base64))
}
function Tick {
    $result = Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10')
    if (!$result.met) { throw 'Player frames did not advance' }
}
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode first.' }
Invoke-Editor 'set_autotick' | Out-Null
Invoke-Editor 'clear_console' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    Eval 'UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();' | Out-Null
    Tick
    foreach ($case in @(@('normal', 0, 'basic', 5), @('fast', 1, 'basic', 5), @('skip', 2, 'basic', 5), @('complex-six', 0, 'complex', 6), @('retained', 0, 'retained', 5), @('zero', 0, 'zero', 5), @('tools', 0, 'tools', 5))) {
        $name=$case[0]; $mode=$case[1]; $scenario=$case[2]; $count=$case[3]
        Eval "return TheCall.Editor.SettlementVisualAcceptance.Prepare(`"$scenario`",$count);" | Out-Null
        $layout=Eval @'
var op=UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>();
var slots=op.extractionSlots.Where(s=>s.gameObject.activeSelf).ToArray();
var positions=slots.Select(s=>((UnityEngine.RectTransform)s.transform).anchoredPosition.x).ToArray();
var gaps=positions.Skip(1).Select((x,i)=>x-positions[i]-178).ToArray();
if(gaps.Any(x=>x<9) || positions.Last()+178>3109) throw new System.Exception("Extraction seats overlap or reach the main collector");
return Newtonsoft.Json.JsonConvert.SerializeObject(new {count=slots.Length, positions, gaps, columns=op.lab.facility.energyColumns.Length});
'@
        $layout | Set-Content (Join-Path $evidence "$name-layout.json") -Encoding utf8
        $directory=(Join-Path $evidence $name).Replace('\','/')
        Eval "return TheCall.Editor.SettlementVisualAcceptance.Begin(`"$directory`",$mode,`"$scenario`");" | Out-Null
        $deadline=(Get-Date).AddMinutes(3)
        do {
            Start-Sleep -Seconds 2
            $state=Eval 'return TheCall.Editor.SettlementVisualAcceptance.Status;'
            if((Get-Date) -gt $deadline) { throw "Settlement timed out: $name" }
        } while($state -eq 'running')
        if($state -ne 'completed') { throw "Settlement failed: $name $state" }
        Write-Output "PASS $name real button, rules, playback and empty columns at completion"
    }
    # Independent engine-light comparison: keep the same fluid height, change only its Light2D source.
    Eval 'TheCall.Editor.SettlementVisualAcceptance.Prepare(); var v=UnityEngine.Object.FindAnyObjectByType<TheCall.FacilitySceneView>(); v.energyColumns[1].Show(0.85f,1);' | Out-Null
    Tick
    Capture 'column-light-on'
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.FacilitySceneView>().energyColumns[1].lamp.enabled=false;' | Out-Null
    Tick
    Capture 'column-light-off'
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.FacilitySceneView>().energyColumns[1].lamp.enabled=true;' | Out-Null
    # Exercise the original reset/cancellation seam in the middle of real playback.
    Eval 'var op=UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>(); op.nextDayButton.onClick.Invoke();' | Out-Null
    $fragments=Invoke-Editor 'wait_for' @('--condition','{"findType":"TheCall.Scoring.ScoringShow","member":"ShardCount","op":"greaterThan","value":0}', '--timeout_s','12', '--on_met','{"pause":true}')
    if(!$fragments.met) {throw 'Cancellation fixture did not reach live glyph fragments'}
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.TheCallPresentation>().Perform(TheCall.PresentationAction.ReturnToTitle);' | Out-Null
    Eval 'UnityEditor.EditorApplication.isPaused=false;' | Out-Null
    Tick
    $cancelled=Eval 'return !TheCall.Scoring.ScoringShow.IsPlaying && UnityEngine.Object.FindObjectsByType<TheCall.Scoring.ScoringGlyphShard>().Length==0 && UnityEngine.Object.FindAnyObjectByType<TheCall.FacilitySceneView>(UnityEngine.FindObjectsInactive.Include).energyColumns.All(c=>c.Fill==0 && c.Glow==0);'
    if(!$cancelled) { throw 'Cancellation left columns, fragments or playback alive' }
    $console=Invoke-Editor 'console' @('--tail','30','--level','error')
    $console | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'runtime-console.json') -Encoding utf8
    if($console.entries.Count -gt 0) { throw 'Unity runtime errors' }
    Write-Output 'PASS cancellation clears fragments and lights; zero runtime errors'
} finally {
    if (!$KeepPlaying) {
        Invoke-Editor 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    }
}
python -X utf8 (Join-Path $PSScriptRoot 'audit-settlement-visuals.py')
if($LASTEXITCODE -ne 0) { throw 'Settlement visual audit failed' }
