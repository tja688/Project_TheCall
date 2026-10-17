#Requires -Version 7.3
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $project 'docs/audit/2026-10-17/facility'
function Invoke-Editor([string]$Name,[string[]]$Arguments=@()) {
    $raw=& unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response=($raw -join "`n") | ConvertFrom-Json
    if($LASTEXITCODE -ne 0 -or !$response.success) {throw ($response | ConvertTo-Json -Depth 8)}
    if($Name -eq 'eval' -and !$response.data.result.success) {throw ($response.data.result | ConvertTo-Json -Depth 8)}
    return $response.data.result
}
function Eval([string]$code) {(Invoke-Editor 'eval' @($code,'30000')).result}
if(Eval 'return UnityEditor.EditorApplication.isPlaying;') {throw 'Run aspect checks after the other player runs.'}
$prepare=@'
var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var window=UnityEditor.EditorWindow.GetWindow(type);
UnityEditor.SessionState.SetInt("FacilityQaOriginalSizeIndex",(int)type.GetProperty("selectedSizeIndex",flags).GetValue(window));
return "saved original Game view selection";
'@
Eval $prepare | Out-Null
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
$report=[Collections.Generic.List[object]]::new()
try {
    Eval 'return TheCall.Editor.FacilityArtAcceptance.Fixture(8);' | Write-Output
    foreach($size in @(@(1280,720),@(1920,1200),@(2560,1080))) {
        $width=$size[0]; $height=$size[1]
        $set=@"
var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var window=UnityEditor.EditorWindow.GetWindow(type);
type.GetMethod("SetCustomResolution",flags).Invoke(window,new object[]{new UnityEngine.Vector2($width,$height),"Facility art QA"});
window.Repaint();
return "requested $width x $height";
"@
        Eval $set | Out-Null
        $condition=@{member='UnityEngine.Screen.width';op='equals';value=$width} | ConvertTo-Json -Compress
        $wait=Invoke-Editor 'wait_for' @('--condition',$condition,'--timeout_s','10')
        if(!$wait.met) {throw "Game view did not render requested width $width"}
        $condition=@{member='UnityEngine.Screen.height';op='equals';value=$height} | ConvertTo-Json -Compress
        $wait=Invoke-Editor 'wait_for' @('--condition',$condition,'--timeout_s','10')
        if(!$wait.met) {throw "Game view did not render requested height $height"}
        Invoke-Editor 'wait_for' @('--condition','{"member":"UnityEngine.Time.frameCount","op":"changed"}','--timeout_s','10') | Out-Null
        $actual=Eval @'
var op=UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>();
var slot=op.cageSlots.First(s=>s.gameObject.activeInHierarchy);
var rect=(UnityEngine.RectTransform)slot.transform;
var point=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<TheCall.MonsterSlotView>()!=slot) throw new System.Exception("Aspect changed cage hit alignment");
return new {width=UnityEngine.Screen.width,height=UnityEngine.Screen.height,rayAligned=true,renderer=((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).GetRenderer(0).GetType().Name};
'@
        $report.Add($actual)
        $capture=Invoke-Editor 'capture_game_view' @('--source','screen','--width',[string]$width,'--height',[string]$height)
        [IO.File]::WriteAllBytes((Join-Path $evidence "aspect-$width-$height.png"),[Convert]::FromBase64String($capture.base64))
        Write-Output "PASS actual render and ray alignment at $width x $height"
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'aspects.json') -Encoding utf8
} finally {
    $restore=@'
var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var window=UnityEditor.EditorWindow.GetWindow(type);
type.GetProperty("selectedSizeIndex",flags).SetValue(window,UnityEditor.SessionState.GetInt("FacilityQaOriginalSizeIndex",0));
window.Repaint();
return "restored original Game view selection";
'@
    Eval $restore | Out-Null
    Invoke-Editor 'editor_stop' | Out-Null
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
}
